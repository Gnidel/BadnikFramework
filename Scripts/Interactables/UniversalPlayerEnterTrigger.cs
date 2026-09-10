using System;
using Godot;

public partial class UniversalPlayerEnterTrigger : Area3D
{
    public override void _Ready()
    {
        this.BodyEntered += this.OnBodyEnter;
    }

    public override void _ExitTree()
    {
        this.BodyEntered -= this.OnBodyEnter;
    }

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            EmitSignal(SignalName.OnPlayerEnter);
            EmitSignal(SignalName.OnPlayerEnterWithRecognition, player);
        }
    }

    [Signal]
    public delegate void OnPlayerEnterEventHandler();

    [Signal]
    public delegate void OnPlayerEnterWithRecognitionEventHandler(PlayerController player);
}
