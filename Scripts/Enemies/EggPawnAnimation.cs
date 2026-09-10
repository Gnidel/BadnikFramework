using System;
using Godot;

public partial class EggPawnAnimation : Node
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public AnimationPlayer AnimationPlayer;

    [Export]
    public RigidBody3D RigidBody;

    [Export]
    public float MinChaseDistance = 30;

    [Export]
    public float MaxChaseDistance = 100;

    [Export]
    public float MinFireDot = 0.99f; //   How much "forward" should be to player to aim again

    [Export]
    public float Speed = 10;

    [Export]
    public GpuParticles3D ChargeParticles;

    [Export]
    public GpuParticles3D ChargeBall;

    [Export]
    public GpuParticles3D FireParticle;

    private double ChargeTimer = 0;
    private double FireTimer = 0;
    private double ChaseTimer = 0;

    [Export]
    public double FireInterval = 0.1f;

    [Export]
    public int BulletsPerCharge = 10;
    private int BulletsFiredInThisCharge;

    [Export]
    public StuffSpawner BulletSpawner;

    [Export]
    public AudioStreamPlayer3D GunAudio;

    [Export]
    public AudioStream ChargeSoundEffect;

    [Export]
    public AudioStream FireSoundEffect;

    private enum EggPawnState
    {
        CHASE,
        AIM,
        FIRE,
        DEAD,
    }

    private EggPawnState State = EggPawnState.CHASE;
    private PlayerController CurrentTarget = null;

    public override void _PhysicsProcess(double delta)
    {
        if (EnemyControl.HP <= 0)
        {
            AnimationPlayer.Play("epg_damage_roll");
            State = EggPawnState.DEAD;
        }

        switch (State)
        {
            case EggPawnState.CHASE:
                Chase(delta);
                break;
            case EggPawnState.AIM:
                Aim(delta);
                break;
            case EggPawnState.FIRE:
                Fire(delta);
                break;
            case EggPawnState.DEAD:
                break;
        }
    }

    void Chase(double delta)
    {
        if (CurrentTarget == null)
        {
            FindTarget();
        }

        var distanceToTarget = CurrentTarget.GlobalPosition.DistanceTo(
            this.RigidBody.GlobalPosition
        );
        if (distanceToTarget > MaxChaseDistance)
        {
            AnimationPlayer.Play("epg_idle");
            this.RigidBody.LinearVelocity = this.RigidBody.LinearVelocity.Project(
                -EnemyControl.Gravity
            );
            return;
        }

        bool lookingAtTarget =
            (-this.RigidBody.Basis.Z).Dot(
                (CurrentTarget.GlobalPosition - this.RigidBody.GlobalPosition)
                    .ProjectOnPlane(this.RigidBody.Basis.Y)
                    .Normalized()
            ) > MinFireDot;

        if (
            distanceToTarget < MinChaseDistance // If close enough
            && lookingAtTarget
            && ChaseTimer >= 0f
        )
        {
            this.RigidBody.LookAt(
                Vector3Utils.ProjectOnPlane(
                    CurrentTarget.GlobalPosition,
                    -EnemyControl.Gravity.Normalized()
                )
                    + Vector3Utils.Project(
                        this.RigidBody.GlobalPosition,
                        -EnemyControl.Gravity.Normalized()
                    ),
                -EnemyControl.Gravity.Normalized()
            );

            AnimationPlayer.Play("epg_shot_pose");
            State = EggPawnState.AIM;

            ChargeParticles.Restart();
            ChargeParticles.Emitting = true;
            ChargeBall.Restart();
            ChargeBall.Emitting = true;

            GunAudio.Stream = ChargeSoundEffect;
            GunAudio.Play(0);

            ChargeTimer = 0;
            ChaseTimer = 0;

            return;
        }
        ChaseTimer += delta;
        AnimationPlayer.Play("epg_walk");

        this.RigidBody.LookAt(
            Vector3Utils.ProjectOnPlane(
                CurrentTarget.GlobalPosition,
                -EnemyControl.Gravity.Normalized()
            )
                + Vector3Utils.Project(
                    this.RigidBody.GlobalPosition,
                    -EnemyControl.Gravity.Normalized()
                ),
            -EnemyControl.Gravity.Normalized()
        );

        this.RigidBody.LinearVelocity = this.RigidBody.LinearVelocity =
            this.RigidBody.LinearVelocity.Project(-EnemyControl.Gravity)
            + -this.RigidBody.Basis.Z * Speed;
    }

    void Aim(double delta)
    {
        ChargeTimer += delta;
        this.RigidBody.LinearVelocity = this.RigidBody.LinearVelocity.Project(
            -EnemyControl.Gravity.Normalized()
        );
        if (ChargeTimer > ChargeParticles.Lifetime)
        {
            FireTimer = 0;
            BulletsFiredInThisCharge = 0;
            State = EggPawnState.FIRE;
        }
    }

    void Fire(double delta)
    {
        FireTimer += delta;
        this.RigidBody.LinearVelocity = this.RigidBody.LinearVelocity.Project(
            -EnemyControl.Gravity.Normalized()
        );
        if (FireTimer > FireInterval)
        {
            FireTimer -= FireInterval;
            BulletSpawner.Spawn();
            BulletsFiredInThisCharge++;

            GunAudio.Stream = FireSoundEffect;
            if (GunAudio.GetPlaybackPosition() > 0.25f || !GunAudio.Playing)
            {
                GunAudio.Play(0);
            }

            if (BulletsFiredInThisCharge > BulletsPerCharge)
            {
                State = EggPawnState.CHASE;
            }
        }
    }

    void FindTarget()
    {
        foreach (var player in PlayerController.Instances)
        {
            if (
                CurrentTarget == null
                || player.GlobalPosition.DistanceTo(this.RigidBody.GlobalPosition)
                    < CurrentTarget.GlobalPosition.DistanceTo(this.RigidBody.GlobalPosition)
            )
            {
                CurrentTarget = player;
            }
        }
    }
}
