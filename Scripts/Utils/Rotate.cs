using System;
using Godot;

public partial class Rotate : Node3D
{
    [Export]
    public Vector3 RelativeAxis = Vector3.Forward;

    [Export]
    public float RadSpeedPerSecond = Mathf.DegToRad(180f);

    public override void _Process(double delta)
    {
        this.Rotate(
            this.Basis.GetRotationQuaternion() * RelativeAxis,
            RadSpeedPerSecond * (float)delta
        );
    }
}
