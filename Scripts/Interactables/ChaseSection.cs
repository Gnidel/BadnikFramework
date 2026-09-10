using System;
using Godot;

public partial class ChaseSection : Node3D
{
    // NOTE: This is FUNDAMENTALLY INCOMPATIBLE WITH MULTIPLAYER. It works only for the first one.
    // If you want this to spawn for each player, spawn this multiple times for each player once they enter the section
    // and set the player manually.

    [Export]
    public Path3D Path;

    [Export]
    public PathFollow3D Follower;

    //[Export] public float MinDistance = 50;
    //[Export] public float MaxDistance = 50;
    [Export]
    public float Distance = 10;

    [Export]
    public float InBetweenSpeed = 20f;

    [Export]
    public float RubberbandSpeed = 150f;

    public PlayerController Player;

    public override void _Ready()
    {
        Follower.ProgressRatio = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player == null)
            return;
        if (Player.PlayerDamage.IsDead)
        {
            Player = null;
            return;
        }

        var offset = Path.Curve.GetClosestOffset(
            Path.GlobalTransform.AffineInverse() * Player.GlobalPosition
        );
        var nextProgress = Mathf.Clamp(
            offset + Distance,
            0,
            Path.Curve.GetBakedLength() - Mathf.Epsilon
        );
        if (Follower.Progress < nextProgress)
        {
            Follower.Progress = nextProgress;
        }
    }

    public void OnStartBodyEnter(Node3D other)
    {
        if (Player != null)
            return;
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null && !player.NpcPartnerControl.IsNpc)
        {
            Player = player;
            Follower.ProgressRatio = 0;
        }
    }

    public void OnEndBodyEnter(Node3D other)
    {
        if (Player == null)
            return;
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null && !player.NpcPartnerControl.IsNpc && player == Player)
        {
            Player = null;
            Follower.ProgressRatio = 1;
        }
    }
}
