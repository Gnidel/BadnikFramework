using System;
using System.Collections.Generic;
using Godot;

public partial class GrassController : Node
{
    [Export]
    public Mesh GrassMesh;

    [Export]
    public int Count = 1000;

    [Export]
    public Material GrassMaterial;

    [Export]
    public MeshInstance3D[] MeshInstancesToPopulate;
    private List<MultiMeshInstance3D> multiMeshes = new List<MultiMeshInstance3D>();
    private Random _rng = new Random();

    [Export]
    bool ShowOnMobile = false;

    [Export]
    bool ShowOnNonMobile = true;

    public override void _Ready()
    {
        bool isMobile = (OS.GetName() == "Android" || OS.GetName() == "iOS");
        if ((!ShowOnMobile && isMobile) || (!ShowOnNonMobile && !isMobile))
        {
            this.QueueFree();
            return;
        }
        MakeAndPopulateMultiMeshes();
    }

    void MakeAndPopulateMultiMeshes()
    {
        foreach (var meshInstance in MeshInstancesToPopulate)
        {
            MultiMeshInstance3D newMultimesh = new MultiMeshInstance3D();
            meshInstance.AddChild(newMultimesh);
            newMultimesh.Position = Vector3.Zero;

            var mm = new MultiMesh();
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            mm.Mesh = GrassMesh;
            mm.InstanceCount = Count;
            newMultimesh.Multimesh = mm;

            if (GrassMaterial != null)
                newMultimesh.MaterialOverride = GrassMaterial.Duplicate() as Material;

            PopulateMultiMesh(mm, meshInstance);
            multiMeshes.Add(newMultimesh);
        }
    }

    void PopulateMultiMesh(MultiMesh mm, MeshInstance3D meshInstance)
    {
        var mesh = meshInstance.Mesh;
        if (mesh == null)
            return;

        // --- 1. Collect all triangles from all surfaces ---
        var triangles =
            new List<(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)>();
        var areas = new List<float>();
        float totalArea = 0f;

        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            var arrays = mesh.SurfaceGetArrays(s);

            var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();

            // Fallback: no index buffer → treat sequential verts as triangles
            if (indices == null || indices.Length == 0)
            {
                indices = new int[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                    indices[i] = i;
            }

            bool hasNormals = normals != null && normals.Length == verts.Length;

            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int i0 = indices[i],
                    i1 = indices[i + 1],
                    i2 = indices[i + 2];
                Vector3 a = verts[i0],
                    b = verts[i1],
                    c = verts[i2];

                Vector3 na,
                    nb,
                    nc;
                if (hasNormals)
                {
                    na = normals[i0];
                    nb = normals[i1];
                    nc = normals[i2];
                }
                else
                {
                    // Compute face normal as fallback
                    Vector3 fn = (b - a).Cross(c - a).Normalized();
                    na = nb = nc = fn;
                }

                float area = (b - a).Cross(c - a).Length() * 0.5f;
                if (area < 1e-8f)
                    continue;

                triangles.Add((a, b, c, na, nb, nc));
                areas.Add(area);
                totalArea += area;
            }
        }

        if (triangles.Count == 0 || totalArea <= 0f)
            return;

        // --- 2. Build a CDF for area-weighted triangle sampling ---
        var cdf = new float[areas.Count];
        float cumulative = 0f;
        for (int i = 0; i < areas.Count; i++)
        {
            cumulative += areas[i] / totalArea;
            cdf[i] = cumulative;
        }

        // --- 3. Place instances ---
        for (int inst = 0; inst < mm.InstanceCount; inst++)
        {
            // Pick a triangle proportional to area
            float r = (float)_rng.NextDouble();
            int triIdx = Array.BinarySearch(cdf, r);
            if (triIdx < 0)
                triIdx = ~triIdx;
            triIdx = Mathf.Clamp(triIdx, 0, triangles.Count - 1);

            var (a, b, c, na, nb, nc) = triangles[triIdx];

            // Uniform random point on the triangle (barycentric)
            float u = (float)_rng.NextDouble();
            float v = (float)_rng.NextDouble();
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }
            float w = 1f - u - v;

            Vector3 pos = a * w + b * u + c * v;
            Vector3 normal = (na * w + nb * u + nc * v).Normalized();

            // --- 4. Build a Transform3D aligned to the surface normal ---
            // Random rotation around the normal axis
            float yaw = (float)(_rng.NextDouble() * Mathf.Pi * 2.0);

            // Tangent: pick an arbitrary perpendicular to the normal
            Vector3 up = Mathf.Abs(normal.Dot(Vector3.Up)) < 0.99f ? Vector3.Up : Vector3.Right;
            Vector3 tangent = normal.Cross(up).Normalized();
            Vector3 bitangent = normal.Cross(tangent).Normalized();

            // Rotate tangent around normal by yaw
            Vector3 rotTangent = tangent * Mathf.Cos(yaw) + bitangent * Mathf.Sin(yaw);
            Vector3 rotBitangent = normal.Cross(rotTangent).Normalized();

            // Grass stands up along Y in model space → map model-Y to surface normal
            // Basis columns: X=rotTangent, Y=normal, Z=rotBitangent
            var basis = new Basis(rotTangent, normal, rotBitangent);

            mm.SetInstanceTransform(inst, new Transform3D(basis, pos));
        }
    }

    public override void _Process(double delta)
    {
        if (PlayerController.Instances.Count == 0)
            return;
        PlayerController mainPlayer = null;
        foreach (var p in PlayerController.Instances)
        {
            if (!p.NpcPartnerControl.IsNpc)
            {
                mainPlayer = p;
                break;
            }
        }
        if (mainPlayer == null)
        {
            return;
        }
        foreach (var multiMesh in multiMeshes)
        {
            multiMesh.MaterialOverride.Set("shader_parameter/PlayerPos", mainPlayer.GlobalPosition);
        }
    }
}
