using System;
using Godot;

public partial class EnemySpinner : RigidBody3D
{
    [Export]
    public int HP = 1;

    [Export]
    public double DamageCooldown;

    [Export]
    public Hitbox Hitbox;

    [Export]
    public Hurtbox Hurtbox;

    [Export]
    public Node3D HomingTarget;

    [Export]
    public AudioStream DamageSoundEffect;

    [Export]
    public AudioStreamPlayer3D AudioSource;

    [Export]
    public PackedScene ExplosionParticle;

    [Export]
    public double MinDeathTime = 0.5f;

    [Export]
    public double MaxDeathTime = 2;

    private bool isDead;
    private double deathTimer;

    public void OnHitboxEnter(Hitbox hitbox, Hurtbox hurtbox)
    {
        if (!isDead && hitbox.CanDamageEnemy)
        {
            this.Hitbox.SetAllConditionsToFalse();
            this.Hitbox.CanDamageEnemy = true;
            hitbox.NotifySuccessfulHit(hurtbox);

            hurtbox.Visible = false;
            HomingTarget.Visible = false;

            HP -= 1;
            AudioSource.Stream = DamageSoundEffect;
            AudioSource.Play();

            if (HP <= 0)
            {
                StartDying(hitbox, hurtbox);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (isDead)
        {
            deathTimer += delta;
            if (deathTimer > MaxDeathTime)
            {
                Explode();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!isDead)
        {
            this.LinearVelocity = this.LinearVelocity.Lerp(Vector3.Zero, (float)delta);
        }
        else
        {
            this.LinearVelocity = this.LinearVelocity.Normalized() * 100f;
        }
    }

    private void StartDying(Hitbox hitbox, Hurtbox hurtbox)
    {
        this.GravityScale = 0;
        isDead = true;

        this.LinearVelocity = (hurtbox.GlobalPosition - hitbox.GlobalPosition).Normalized() * 100f;
    }

    public void OnCollission(Node other)
    {
        if (isDead && deathTimer > MinDeathTime)
        {
            Explode();
        }
    }

    private void Explode()
    {
        var explosionInstance = ExplosionParticle.Instantiate<GpuParticles3D>();
        this.GetParent().AddChild(explosionInstance);
        explosionInstance.GlobalPosition = this.GlobalPosition;
        explosionInstance.Emitting = true;

        this.QueueFree();
    }
}
