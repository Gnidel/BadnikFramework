using System;
using Godot;

public partial class Autoscroller : Node3D
{
    [Export]
    public PathFollow3D PathFollow;

    [Export]
    public Node3D Trigger;

    [Export]
    public Node3D Border;

    [Export]
    public float Speed = 30;

    private bool IsAutoscrolling;

    public override void _Ready()
    {
        StopAutoscrolling();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsAutoscrolling)
            return;

        PathFollow.Progress += Speed * (float)delta;
    }

    public void StartAutoscrolling()
    {
        IsAutoscrolling = true;
        Border.Visible = true;
        Border.ProcessMode = ProcessModeEnum.Inherit;
        Trigger.ProcessMode = ProcessModeEnum.Disabled;
        Trigger.Visible = false;
    }

    public void StopAutoscrolling()
    {
        IsAutoscrolling = false;
        Border.Visible = false;
        Border.ProcessMode = ProcessModeEnum.Disabled;
    }

    public void BodyEntered(Node3D body)
    {
        if (body.GetNodeOrNull<PlayerController>(".") != null)
        {
            this.CallDeferred("StartAutoscrolling"); // Deferred call because there's disabling trigger.
        }
    }
}
