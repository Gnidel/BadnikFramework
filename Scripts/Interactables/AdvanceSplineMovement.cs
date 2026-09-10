using System;
using Godot;

public partial class AdvanceSplineMovement : Node
{
    [Export]
    public PathFollow3D PathFollow;

    [Export]
    public float Speed;

    public override void _PhysicsProcess(double delta)
    {
        PathFollow.Progress += Speed * (float)delta;
    }
}
