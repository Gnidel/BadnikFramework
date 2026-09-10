using System;
using Godot;

public partial class WaitForAggroBehavior : Node
{
    [Export]
    public EnemyBehaviorManager BehaviorManager;

    [Export]
    public Node NextPhase;

    [Export]
    public bool AddTarget;

    [Signal]
    public delegate void OnAggroEventHandler();

    private bool active = false;

    public override void _Ready()
    {
        active = false;
    }

    public override void _Process(double delta)
    {
        if (!active)
        {
            return;
        }
    }

    public void OnTriggerEnter(Node3D other)
    {
        if (active || !this.IsProcessing())
        {
            return;
        }
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            active = true;

            BehaviorManager.SetPhase(NextPhase);
            if (AddTarget && this.BehaviorManager.EnemyControl.HomingTarget != null)
            {
                var cam = PlayerCamera.Instances[player.PlayerID];
                cam.Mode = PlayerCamera.CameraMode.LookAtPoint;
                cam.Point = this.GetNode<Node3D>("../../..");
                cam.CameraTargets.Add(this.BehaviorManager.EnemyControl.HomingTarget);
            }
            EmitSignal(SignalName.OnAggro);
        }
    }
}
