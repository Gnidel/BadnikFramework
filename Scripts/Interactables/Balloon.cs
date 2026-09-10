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
    public MeshInstance3D BalloonMesh;

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
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player == null)
            return;

        var playerActions = player.GetNodeOrNull<PlayerActions>("./PlayerControl/Actions");
        if (playerActions != null)
        {
            playerActions.ResetActions();
        }

        var rotatedEjectVelocity =
            new Quaternion(
                Vector3.Forward,
                player.LinearVelocity.ProjectOnPlane(this.GlobalBasis.Y).Normalized()
            ).Normalized() * RelativeEjectVelocity;
        player.LinearVelocity = rotatedEjectVelocity;

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
        this.QueueFree();
    }
}
