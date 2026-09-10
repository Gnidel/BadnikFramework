using System;
using Godot;

public partial class RunawayPathBehavior : Node
{
    [Export]
    public RigidBody3D RigidBody;

    [Export]
    public PathFollow3D PathFollow;

    [Export]
    public float ProgressSpeed = 5;

    [Export]
    public float CatchupSpeed = 5;

    [Export]
    public float CaughtUpDistance = 0.5f;

    public override void _PhysicsProcess(double delta)
    {
        RigidBody.LinearVelocity = Vector3.Zero;
        var newProgress = PathFollow.Progress;
        newProgress += (float)delta * ProgressSpeed;

        PathFollow.Progress = newProgress;

        if (
            this.RigidBody.GlobalPosition.DistanceTo(this.PathFollow.GlobalPosition)
            > CaughtUpDistance
        )
        {
            this.RigidBody.GlobalPosition = this.RigidBody.GlobalPosition.Lerp(
                PathFollow.GlobalPosition,
                (float)delta * CatchupSpeed
            );
        }
        else
        {
            this.RigidBody.GlobalPosition = PathFollow.GlobalPosition;
        }
    }
}
