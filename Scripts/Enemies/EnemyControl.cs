using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

public partial class EnemyControl : Node
{
    [Export]
    public int HP = 1;

    [Export]
    public int MaxHP = 1;

    [Export]
    public HealthBarControl HealthBar;

    [Export]
    public double DamageCooldown;

    [Export]
    public Hitbox Hitbox;

    [Export]
    public Hurtbox Hurtbox;

    [Export]
    public Node3D HomingTarget;

    [Export]
    public RigidBody3D RigidBody;

    [Export]
    public MeshInstance3D MeshInstance;

    [Export]
    public AudioStream DamageSoundEffect;

    [Export]
    public GpuParticles3D DamageParticles;

    [Export]
    public AudioStreamPlayer3D AudioSource;

    [Export]
    public PackedScene ExplosionParticles; // TODO: Should be of type MultiParticlePlayer

    [Export]
    public GpuParticles3D DeathAnticipationParticle;

    [Export]
    public double MinDeathTime = 0.5f;

    [Export]
    public double MaxDeathTime = 2;

    [Export]
    public EnemyBehaviorManager BehaviorManager;

    [Export]
    public Node DeathPhase;

    [Export]
    public Vector3 Gravity = -Vector3.Up; // Keep it even for flying enemies, to make them keep track where "up" is in Lost World gravity.

    [Export]
    public float GravityScale = 0f;

    [Export]
    public bool IgnoreGravityTriggers = false;

    [Export]
    public bool DefaultGravityIsInitialDown = false;

    [Export]
    public bool DontCull = false; // Don't cull the enemy no matter what its distance from player is.

    [Export]
    public bool DontExplodeWhenStageFinished = false;

    [Export]
    public bool DefaultDyingBehavior = true;

    private bool isDead;
    private double deathTimer;
    private double damageTimer;

    public static List<EnemyControl> Instances = new List<EnemyControl>();

    public override void _EnterTree()
    {
        Instances.Add(this);

        if (
            ExplosionParticles != null
            && ResourceLoader.LoadThreadedGetStatus(ExplosionParticles.ResourcePath)
                == ResourceLoader.ThreadLoadStatus.InvalidResource
        )
        {
            ResourceLoader.LoadThreadedRequest(ExplosionParticles.ResourcePath);
        }
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
    }

    public override void _Ready()
    {
        if (HealthBar != null)
        {
            HealthBar.SetMaxHP(MaxHP);
            HealthBar.SetHP(HP);
        }

        if (DefaultGravityIsInitialDown)
        {
            this.Gravity = -RigidBody.GlobalBasis.Y * this.Gravity.Length();
        }

        if (this.BehaviorManager != null)
        {
            this.BehaviorManager.EnemyControl = this;
        }
    }

    public void OnHitboxEnter(Hitbox hitbox, Hurtbox hurtbox)
    {
        if (!isDead && hitbox.CanDamageEnemy)
        {
            this.Hitbox.SetAllConditionsToFalse();
            this.Hitbox.CanDamageEnemy = true;
            if (hitbox.ResponsiblePlayer != null)
            {
                this.Hitbox.ResponsiblePlayer = hitbox.ResponsiblePlayer;
            }

            if (damageTimer < DamageCooldown)
                return;
            hitbox.NotifySuccessfulHit(hurtbox);

            hurtbox.Visible = false;
            HomingTarget.Visible = false;

            damageTimer = 0;

            if (!hitbox.IsInstaKill)
            {
                HP -= 1;
            }
            else
            {
                HP = 0;
                damageTimer = MaxDeathTime;
            }
            if (HealthBar != null)
            {
                HealthBar.SetHP(HP);
            }
            AudioSource.Stream = DamageSoundEffect;
            AudioSource.Play();
            if (DamageParticles != null)
            {
                DamageParticles.GlobalPosition = hitbox.GlobalPosition.Lerp(
                    hurtbox.GlobalPosition,
                    0.5f
                );
                DamageParticles.Emitting = true;
            }

            if (HP <= 0)
            {
                StartDying(hitbox, hurtbox);
                EmitSignal(SignalName.OnDeath, this);
            }
            EmitSignal(SignalName.OnDamage, this);
        }
    }

    public override void _Process(double delta)
    {
        if (StageData.Instance.IsFinished == true && !DontExplodeWhenStageFinished)
        {
            this.Explode();
            return;
        }

        if (isDead)
        {
            deathTimer += delta;
            if (DefaultDyingBehavior)
            {
                if (deathTimer > MaxDeathTime)
                {
                    Explode();
                    if (HealthBar != null)
                    {
                        HealthBar.FadeOut();
                    }
                }
            }
        }
        else
        {
            damageTimer += delta;
            if (damageTimer > DamageCooldown)
            {
                if (Hurtbox != null)
                    Hurtbox.Visible = true;
                if (HomingTarget != null)
                    HomingTarget.Visible = true;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (RigidBody == null)
            return;
        if (!isDead)
        {
            if (GravityScale > 0)
            {
                RigidBody.LinearVelocity =
                    RigidBody.LinearVelocity.Lerp(Vector3.Zero, (float)delta)
                    + Gravity.Normalized() * GravityScale * (float)delta;
            }
        }
        else
        {
            if (!RigidBody.LinearVelocity.IsFinite() || RigidBody.LinearVelocity.IsZeroApprox())
                RigidBody.LinearVelocity =
                    this.RigidBody.GlobalBasis.Y + this.RigidBody.GlobalBasis.Z;
            RigidBody.LinearVelocity = RigidBody.LinearVelocity.Normalized() * 100f;
            if (DeathAnticipationParticle != null)
            {
                DeathAnticipationParticle.Emitting = true;
            }
        }
    }

    public void StartDying(Hitbox hitbox, Hurtbox hurtbox)
    {
        this.BehaviorManager.SetPhase(DeathPhase);
        isDead = true;
        DontCull = true;

        if (DefaultDyingBehavior && RigidBody != null)
        {
            RigidBody.GravityScale = 0;

            RigidBody.LinearVelocity =
                (hurtbox.GlobalPosition - hitbox.GlobalPosition).Normalized() * 100f;
            RigidBody.AngularVelocity =
                (hurtbox.GlobalPosition - hitbox.GlobalPosition).Normalized() * 100f;
        }
    }

    public void OnCollission(Node other)
    {
        if (isDead && deathTimer > MinDeathTime && DefaultDyingBehavior)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (RigidBody == null)
            return;
        if (
            ExplosionParticles != null
            && ResourceLoader.LoadThreadedGetStatus(ExplosionParticles.ResourcePath)
                == ResourceLoader.ThreadLoadStatus.Loaded
        )
        {
            var explosionInstance = ExplosionParticles.Instantiate<MultiParticlePlayer>();
            RigidBody.GetParent().AddChild(explosionInstance);
            explosionInstance.GlobalPosition = RigidBody.GlobalPosition;
            explosionInstance.Play();
        }

        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.CameraTargets.Remove(this.RigidBody);
            if (cam.Value.Point != null && cam.Value.Point == this.RigidBody)
            {
                cam.Value.Mode = PlayerCamera.CameraMode.Free;
                cam.Value.Point = null;
            }
        }

        RigidBody.QueueFree();
    }

    [Signal]
    public delegate void OnDamageEventHandler(EnemyControl damagedEnemy);

    [Signal]
    public delegate void OnDeathEventHandler(EnemyControl deadEnemy);
}
