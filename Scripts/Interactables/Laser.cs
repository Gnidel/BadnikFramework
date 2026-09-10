using System;
using Godot;

public partial class Laser : RayCast3D
{
    [Export]
    public MeshInstance3D[] BeamMeshes = new MeshInstance3D[0];

    [Export]
    public Hitbox Hitbox;

    [Export]
    public CollisionObject3D[] CollissionExceptions = new CollisionObject3D[0];

    [Export]
    public CollisionShape3D HitboxShape;

    [Export]
    public Vector3 UvScroll;

    [Export]
    public GpuParticles3D StartParticles;

    [Export]
    public GpuParticles3D EndParticles;

    [Export]
    public bool LaserActive = true;

    public override void _Ready()
    {
        foreach (var ex in CollissionExceptions)
        {
            AddExceptionRid(ex.GetRid());
        }

        SetLaserActive(LaserActive);
    }

    public void SetLaserActive(bool active)
    {
        LaserActive = active;

        foreach (var beamMesh in BeamMeshes)
        {
            beamMesh.Visible = active;
        }

        StartParticles.Visible = active;
        EndParticles.Visible = active;

        Hitbox.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }

    public override void _Process(double delta)
    {
        // TargetPosition is local to the RayCast3D.
        // ToGlobal() correctly accounts for rotation, scale, and parent transforms.
        var hitPoint = ToGlobal(TargetPosition);

        if (IsColliding())
        {
            // GetCollisionPoint() is already in global space.
            hitPoint = GetCollisionPoint();
        }

        // Convert the final endpoint back into the laser's local space.
        var localHitPoint = ToLocal(hitPoint);

        var laserLength = Mathf.Abs(localHitPoint.Y);

        foreach (var beamMesh in BeamMeshes)
        {
            beamMesh.Mesh.Set("height", laserLength);
            beamMesh.Position = localHitPoint / 2.0f;

            var material = beamMesh.GetActiveMaterial(0);

            if (material != null && laserLength > 0.0001f)
            {
                material.Set("uv1_scale", new Vector3(0, 1.0f / laserLength, 0));

                var currentOffset = material.Get("uv1_offset").AsVector3();

                material.Set("uv1_offset", currentOffset + UvScroll * (float)delta);
            }
        }

        HitboxShape.Shape.Set("height", laserLength);
        HitboxShape.Position = localHitPoint / 2.0f;

        EndParticles.GlobalPosition = hitPoint;
    }
}
