using System;
using Godot;

public partial class HomingMissile : RigidBody3D
{
    [Export]
    public PackedScene ExplosionPrefab;

    //public PlayerController OwnerPlayer;
    public ActionGun OwnerPlayerActionGun;
    public Node3D Target;

    [Export]
    public float RotationSpeed = 20f;

    [Export]
    public float Speed = 300f;

    [Export]
    public float ExplodeDistance = 2f;
    private Vector3 TargetPosition;

    [Export]
    public double MaxLifetime = 1f;
    private double lifetimetimer = 0;

    public override void _PhysicsProcess(double delta)
    {
        lifetimetimer += delta;
        if (lifetimetimer > MaxLifetime)
        {
            Explode();
            return;
        }
        if (Target != null && IsInstanceValid(Target) && Target.IsInsideTree())
        {
            this.LookAt(Target.GlobalPosition);
            TargetPosition = Target.GlobalPosition;

            if (this.GlobalPosition.DistanceTo(TargetPosition) < ExplodeDistance)
            {
                Explode();
            }
        }
        this.LinearVelocity = -this.GlobalBasis.Z * Speed;
    }

    public void OnBodyEntered(Node Body)
    {
        Explode();
    }

    void Explode()
    {
        var explosion = ExplosionPrefab.Instantiate<MultiParticlePlayer>();
        GetTree().Root.AddChild(explosion);
        explosion.GlobalPosition = this.GlobalPosition;
        explosion.Play();

        var hitbox = explosion.GetNode<Hitbox>("./Hitbox");
        hitbox.ResponsiblePlayer = this.OwnerPlayerActionGun.Player;
        hitbox.OnSuccessfulHit += (Hitbox hitbox, Hurtbox hurtbox) =>
        {
            this.OwnerPlayerActionGun.OnSuccessfulHit(
                this.OwnerPlayerActionGun.CurrentTargets.Count
            );
        };

        this.QueueFree();
    }
}
