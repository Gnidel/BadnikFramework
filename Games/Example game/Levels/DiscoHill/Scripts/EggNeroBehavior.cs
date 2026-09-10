using System;
using Godot;

public partial class EggNeroBehavior : Node
{
    [Export]
    public Node3D BossTransform;

    [Export]
    public Node3D DiscSpawnPoint;

    [Export]
    public Node3D DiscOutsidePoint;

    [Export]
    public PackedScene DiscTemplate;

    [Export]
    public PackedScene FireParticleTemplate;

    [Export]
    public PackedScene DiscHitboxTemplate;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream DiscSoundEject;

    [Export]
    public AudioStream DiscSoundSpin;

    [Export]
    public ulong Seed;
    private float PhaseTimer;

    [Export]
    public float RotatePhaseTime = 0.5f;

    [Export]
    public float ChargePhaseTime = 1f;

    [Export]
    public float BurnPhaseTime = 0.25f;

    [Export]
    public float LaunchPhaseTime = 1f;

    [Export]
    public Vector3 DiscVelocity = new Vector3(0, 0, 50);

    // Rotate phase
    private Vector3 PrevRotation;
    private Vector3 NextRotation;

    // Charge/burn phase
    private RigidBody3D NewDisc;

    // Launch phase
    [Export]
    public Node3D FiredDiscsContainer;

    private enum EggNeroModes
    {
        Rotate, // Rotate to a new position
        Charge, // Spawn a non-burning disc, move it slightly forward
        Burn, // Start fire on the disc.
        Launch, // Launch the disc and wait for a moment
    }

    private EggNeroModes CurrentPhase = EggNeroModes.Rotate;

    public override void _Ready()
    {
        GD.Seed(Seed);
        StartRotatePhase();
    }

    public override void _PhysicsProcess(double delta)
    {
        PhaseTimer += (float)delta;
        switch (CurrentPhase)
        {
            case EggNeroModes.Rotate:
                RotatePhase();
                break;
            case EggNeroModes.Charge:
                ChargePhase();
                break;
            case EggNeroModes.Burn:
                BurnPhase();
                break;
            case EggNeroModes.Launch:
                LaunchPhase();
                break;
        }
    }

    void StartRotatePhase()
    {
        CurrentPhase = EggNeroModes.Rotate;
        PhaseTimer = 0;

        PrevRotation = BossTransform.RotationDegrees;
        var nextRotationAngle = GD.Randf() * 360 - 180;
        NextRotation = new Vector3(0, 0, nextRotationAngle);
        AudioPlayer.Stream = DiscSoundSpin;
        AudioPlayer.Play(0);
    }

    void RotatePhase()
    {
        BossTransform.RotationDegrees = PrevRotation.Lerp(
            NextRotation,
            PhaseTimer / RotatePhaseTime
        );
        if (PhaseTimer > RotatePhaseTime)
        {
            StartChargePhase();
        }
    }

    void StartChargePhase()
    {
        CurrentPhase = EggNeroModes.Charge;
        PhaseTimer = 0;
        NewDisc = DiscTemplate.Instantiate<RigidBody3D>();
        DiscSpawnPoint.AddChild(NewDisc);
        NewDisc.GlobalTransform = DiscSpawnPoint.GlobalTransform;
    }

    void ChargePhase()
    {
        NewDisc.GlobalPosition = DiscSpawnPoint.GlobalPosition.Lerp(
            DiscOutsidePoint.GlobalPosition,
            PhaseTimer / ChargePhaseTime
        );
        if (PhaseTimer > ChargePhaseTime)
        {
            StartBurnPhase();
        }
    }

    void StartBurnPhase()
    {
        CurrentPhase = EggNeroModes.Burn;
        PhaseTimer = 0;

        var fire = FireParticleTemplate.Instantiate<Node3D>();
        NewDisc.AddChild(fire);
        fire.GlobalTransform = NewDisc.GlobalTransform;

        AudioPlayer.Stream = DiscSoundEject;
        AudioPlayer.Play(0);
    }

    void BurnPhase()
    {
        if (PhaseTimer > BurnPhaseTime)
        {
            StartLaunchPhase();
        }
    }

    void StartLaunchPhase()
    {
        CurrentPhase = EggNeroModes.Launch;
        PhaseTimer = 0;

        var discHitbox = DiscHitboxTemplate.Instantiate<Node3D>();
        NewDisc.AddChild(discHitbox);
        discHitbox.GlobalTransform = NewDisc.GlobalTransform;

        NewDisc.LinearVelocity =
            DiscOutsidePoint.GlobalBasis.GetRotationQuaternion() * DiscVelocity;

        NewDisc.GetParent().RemoveChild(NewDisc);
        NewDisc.GlobalTransform = DiscOutsidePoint.GlobalTransform;
        FiredDiscsContainer.AddChild(NewDisc);
    }

    void LaunchPhase()
    {
        //NewDisc.LinearVelocity = DiscOutsidePoint.Quaternion * DiscVelocity;
        if (PhaseTimer > LaunchPhaseTime)
        {
            StartRotatePhase();
        }
    }
}
