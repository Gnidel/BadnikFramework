using System.Collections.Generic;
using Godot;

#if TOOLS
[Tool]
public partial class MapBaker : Node3D
{
    [Export]
    public Vector3 AabbMin = new Vector3(-10, -1, -10);

    [Export]
    public Vector3 AabbMax = new Vector3(10, 10, 10);

    // Pick a texture size whose aspect ratio matches (AabbMax.X-AabbMin.X) / (AabbMax.Z-AabbMin.Z)
    // otherwise the capture will letterbox instead of exactly filling the texture.
    [Export]
    public Vector2I TextureSize = new Vector2I(1024, 1024);

    [Export]
    public string OutputPath = "res://map_snapshot.png";

    // Only render objects on these visual layers (e.g. terrain layer, not markers/UI)
    [Export(PropertyHint.Layers3DRender)]
    public uint CullMask = 1; // layer 1 only — excludes editor gizmos/grid on layers 21-32

    // --- Heightmap ---
    // Leave empty to skip the heightmap bake entirely.
    [Export]
    public string HeightmapOutputPath = "";

    // Zero = reuse TextureSize.
    [Export]
    public Vector2I HeightmapSize = new Vector2I(0, 0);

    [ExportToolButton("Bake Map Texture")]
    public Callable BakeButton => Callable.From(BakeMapTexture);

    public void BakeMapTexture()
    {
        BakeColorMap();

        if (!string.IsNullOrEmpty(HeightmapOutputPath))
            BakeHeightMap();

        EditorInterface.Singleton.GetResourceFilesystem().Scan();
    }

    private void BakeColorMap()
    {
        Vector3 size = AabbMax - AabbMin;
        var (viewport, camera) = CreateOrthoCapture(TextureSize, size, CullMask);

        RenderingServer.ForceDraw(false);
        RenderingServer.ForceDraw(false);
        Image img = viewport.GetTexture().GetImage();
        img.SavePng(OutputPath);
        viewport.QueueFree();

        GD.Print($"Baked map to {OutputPath}");
    }

    private void BakeHeightMap()
    {
        Vector3 size = AabbMax - AabbMin;
        Vector2I texSize = HeightmapSize == Vector2I.Zero ? TextureSize : HeightmapSize;

        // Height-encoding shader: normalized world-space Y -> grayscale albedo.
        // Normalizing per-vertex and interpolating is equivalent to interpolating
        // world height and normalizing after, since the normalization is affine.
        var shader = new Shader
        {
            Code =
                @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

uniform float height_min;
uniform float height_max;

varying float world_height;

void vertex() {
    world_height = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y;
}

void fragment() {
    float t = clamp((world_height - height_min) / max(height_max - height_min, 0.0001), 0.0, 1.0);
    ALBEDO = vec3(t);
}
",
        };
        var heightMat = new ShaderMaterial { Shader = shader };
        heightMat.SetShaderParameter("height_min", AabbMin.Y);
        heightMat.SetShaderParameter("height_max", AabbMax.Y);

        // Swap every mesh's material override for the height shader, capture, then restore.
        Node sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        var originals = new Dictionary<GeometryInstance3D, Material>();
        CollectAndOverride(sceneRoot, heightMat, originals);

        var (viewport, camera) = CreateOrthoCapture(texSize, size, CullMask);

        RenderingServer.ForceDraw(false);
        RenderingServer.ForceDraw(false);
        Image img = viewport.GetTexture().GetImage();
        viewport.QueueFree();

        foreach (var kvp in originals)
            kvp.Key.MaterialOverride = kvp.Value;

        // Collapse to 8-bit grayscale — background pixels (nothing rendered) land at 0,
        // same as a valid height of AabbMin.Y, so treat 0 as "no data" downstream if that matters.
        img.Convert(Image.Format.L8);
        img.SavePng(HeightmapOutputPath);

        GD.Print($"Baked heightmap to {HeightmapOutputPath}");
    }

    private void CollectAndOverride(
        Node node,
        Material overrideMat,
        Dictionary<GeometryInstance3D, Material> originals
    )
    {
        if (node is GeometryInstance3D gi)
        {
            originals[gi] = gi.MaterialOverride;
            gi.MaterialOverride = overrideMat;
        }
        foreach (Node child in node.GetChildren())
            CollectAndOverride(child, overrideMat, originals);
    }

    private (SubViewport, Camera3D) CreateOrthoCapture(
        Vector2I texSize,
        Vector3 aabbSize,
        uint cullMask
    )
    {
        Vector3 center = (AabbMin + AabbMax) * 0.5f;
        var viewport = new SubViewport
        {
            Size = texSize,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = true,
        };
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Near = 0.05f,
            Far = aabbSize.Y + 10f,
            KeepAspect = Camera3D.KeepAspectEnum.Width,
            Size = aabbSize.X,
            CullMask = cullMask,
        };
        AddChild(viewport);
        viewport.AddChild(camera);
        camera.Current = true;
        Vector3 camPos = new Vector3(center.X, AabbMax.Y + 5f, center.Z);
        // Look straight down (-Y). Use Vector3.Forward as the "up" reference
        // since it's perpendicular to Down — avoids the gimbal issue you'd get
        // trying to use Vector3.Up as the reference here.
        Basis lookBasis = Basis.LookingAt(Vector3.Down, Vector3.Forward);
        camera.GlobalTransform = new Transform3D(lookBasis, camPos);
        camera.ForceUpdateTransform(); // flush the new transform to the renderer now
        return (viewport, camera);
    }
}
#else
public partial class MapBaker : Node3D { }
#endif
