using System;
using Godot;

public partial class NonPlayerSplineMovement : Node
{
    [Export]
    public Path3D Path;

    public override void _PhysicsProcess(double delta)
    {
        if (Path == null)
            return;

        var rb = this.GetParentOrNull<RigidBody3D>();
        if (rb == null)
            return;

        var nearestOffset = Path.Curve.GetClosestOffset(rb.GlobalPosition - Path.GlobalPosition);

        var sample = Path.Curve.SampleBakedWithRotation(nearestOffset);

        var depthOffset = Vector3Utils.Project(
            (sample.Origin + Path.GlobalPosition) - rb.GlobalPosition,
            sample.Basis.X
        );

        var originalVelocity = rb.LinearVelocity.Length();

        rb.LinearVelocity = Vector3Utils.ProjectOnPlane(
            rb.LinearVelocity,
            sample.Basis.X.Normalized()
        );
        rb.LinearVelocity += depthOffset * originalVelocity / 10f;
        //rb.LinearVelocity = Vector3Utils.ProjectOnPlane(Player.VelocityXZ, Player.GroundNormal).Normalized() * originalVelocity;
    }
}
