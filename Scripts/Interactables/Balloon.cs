using System;
using Godot;

public partial class Balloon : Node3D
{
    [Export]
    public PackedScene ExplosionParticles;

    [Export]
    public Vector3 RelativeEjectVelocity = new Vector3(0, 50, -50);

    [Export]
    public Color Color = new Color(0.5f, 0.5f, 0.5f);

    [Export]
    public bool RandomizeColor = true;

    [Export]
    public float SpeedMin;

    [Export]
    public float SpeedMax = Mathf.Inf;

    [Export]
    public bool UseSpeedBounds;

    [Export]
    public double OutOfControlTime;

    [Export]
    public float KeepVelocityTime;

    [Export]
    public float RespawnTime;

    [Export]
    public MeshInstance3D BalloonMesh;

    private bool consumed;

    public override void _Ready()
    {
        if (
            ExplosionParticles != null
            && ResourceLoader.LoadThreadedGetStatus(ExplosionParticles.ResourcePath)
                == ResourceLoader.ThreadLoadStatus.InvalidResource
        )
        {
            ResourceLoader.LoadThreadedRequest(ExplosionParticles.ResourcePath);
        }

        if (RandomizeColor && BalloonMesh != null)
        {
            GD.Seed(this.GetInstanceId());
            var randHue = GD.Randf();
            Color = Color.FromHsv(randHue, 1, 1, 1);
        }
        BalloonMesh.GetActiveMaterial(0).Set("albedo_color", Color);
    }

    public void SetColor(Color color)
    {
        this.Color = color;
        BalloonMesh.GetActiveMaterial(0).Set("albedo_color", color);
    }

    public void OnBodyEnter(Node3D other)
    {
        if (consumed)
            return;

        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player == null)
            return;

        consumed = true;

        var playerActions = player.GetNodeOrNull<PlayerActions>("./PlayerControl/Actions");
        if (playerActions != null)
        {
            playerActions.ResetActions();
        }

        var tangentVelocity = player.LinearVelocity.ProjectOnPlane(this.GlobalBasis.Y);
        var ejectVelocity = RelativeEjectVelocity;
        if (UseSpeedBounds)
        {
            ejectVelocity.Z = -Mathf.Clamp(tangentVelocity.Length(), SpeedMin, SpeedMax);
        }
        var tangentDirection = tangentVelocity.IsZeroApprox()
            ? -player.GlobalBasis.Z.ProjectOnPlane(this.GlobalBasis.Y).Normalized()
            : tangentVelocity.Normalized();
        if (tangentDirection.IsZeroApprox())
            tangentDirection = Vector3.Forward;
        var rotatedEjectVelocity = new Quaternion(
            Vector3.Forward,
            tangentDirection
        ).Normalized() * ejectVelocity;
        player.LinearVelocity = rotatedEjectVelocity;
        if (OutOfControlTime > 0)
        {
            player.PlayerInput.LeftInputTimedLock = Mathf.Max(
                (float)player.PlayerInput.LeftInputTimedLock,
                (float)OutOfControlTime
            );
        }
        if (KeepVelocityTime > 0)
            player.LockVelocity(0, KeepVelocityTime);

        if (ExplosionParticles != null)
        {
            ResourceLoader.LoadThreadedRequest(ExplosionParticles.ResourcePath);
            var explosion = (
                ResourceLoader.LoadThreadedGet(ExplosionParticles.ResourcePath) as PackedScene
            ).Instantiate<Node3D>();

            this.GetParent().AddChild(explosion);
            explosion.GlobalPosition = this.GlobalPosition;

            var mep = explosion.GetNodeOrNull<MultiParticlePlayer>(".");
            if (mep != null)
            {
                mep.Play();
            }
        }
        var hitbox = GetNodeOrNull<Area3D>("Offset/Hitbox");
        if (RespawnTime > 0 && hitbox != null)
        {
            Visible = false;
            hitbox.SetDeferred("monitoring", false);
            hitbox.SetDeferred("monitorable", false);
            RespawnAfterDelay(hitbox);
        }
        else
        {
            QueueFree();
        }
    }

    private async void RespawnAfterDelay(Area3D hitbox)
    {
        await ToSignal(GetTree().CreateTimer(RespawnTime), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(hitbox))
            return;

        Visible = true;
        hitbox.Monitorable = true;
        hitbox.Monitoring = true;
        consumed = false;
    }
}
