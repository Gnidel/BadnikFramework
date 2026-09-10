using System;
using Godot;

public partial class AirBubble : Node
{
    [Export]
    public Vector3 RelativeTargetDistanceFromSpawn = Vector3.Up * 4;

    [Export]
    public float TimeToRepositionAndRescale = 1f;
    private float lifetimeTimer = 0;

    [Export]
    Vector3 TargetScale = Vector3.One;

    public override void _PhysicsProcess(double delta)
    {
        var bubble = this.GetNode<Node3D>("..");
        lifetimeTimer += (float)delta;
        bubble.Scale += Mathf.Clamp((float)delta / TimeToRepositionAndRescale, 0, 1) * TargetScale;
        bubble.GlobalPosition +=
            bubble.GlobalTransform.Basis.GetRotationQuaternion().Normalized()
            * RelativeTargetDistanceFromSpawn
            * Mathf.Clamp((float)delta / TimeToRepositionAndRescale, 0, 1);

        if (lifetimeTimer > TimeToRepositionAndRescale)
        {
            this.ProcessMode = ProcessModeEnum.Disabled;
        }
    }
}
