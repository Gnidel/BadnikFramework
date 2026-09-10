using System;
using Godot;

public partial class Bomb : RigidBody3D
{
    [Export]
    public double MinTimeToExplode = 0.5f;

    [Export]
    public double MaxTimeToExplode = 2f;

    [Export]
    public PackedScene ExplosionParticles; // Should be of type MultiParticlePlayer

    private double explosionTimer = 0;

    public override void _Ready()
    {
        this.BodyEntered += (Node body) => OnBodyEntered(body);
    }

    public override void _PhysicsProcess(double delta)
    {
        explosionTimer += delta;
        if (explosionTimer > MaxTimeToExplode)
        {
            Explode();
        }
    }

    public void OnBodyEntered(Node body)
    {
        if (explosionTimer > MinTimeToExplode && body.GetNodeOrNull<PlayerController>(".") == null)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (ExplosionParticles != null)
        {
            var explosionInstance = ExplosionParticles.Instantiate<MultiParticlePlayer>(); // TODO: It may not be loaded yet. Add background loading for that.
            this.GetParent().AddChild(explosionInstance);
            explosionInstance.GlobalPosition = this.GlobalPosition;
            explosionInstance.Play();
        }
        this.QueueFree();
    }
}
