using System;
using Godot;

public partial class TwoDimensionalTrigger : Area3D
{
    [Export]
    public bool TwoDimensionalMode = false;

    [Export]
    public Vector3 Right = Vector3.Forward;

    [Export]
    public Vector3 Up = Vector3.Up;

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            player.PlayerInput.TwodimensionalMode = this.TwoDimensionalMode;
            player.PlayerInput.Plane2DControlsX = Right;
            player.PlayerInput.Plane2DControlsY = Up;
        }
    }
}
