using System;
using Godot;
using Godot.NativeInterop;

public partial class ActionDrift : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionBoost ActionBoost;

    [Export]
    public ActionGrind ActionGrind;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public bool RequireBoost = false;

    public bool IsDrifting;
    public float DriftDir = 0; // Left == -1, Right == 1
    public float VisualDriftDir = 0; // Left == -1, Right == 1

    [Export]
    public float VisualDirChangeSpeed = 5;

    public Vector3 Inertia = Vector3.Zero;

    [Export]
    public AudioStream DriftSoundEffect;

    [Export]
    public AudioStreamPlayer3D DriftSoundEffectPlayer;

    [Export]
    public float DriftMinInputDot = 0.5f;

    [Export]
    public float DriftMinTurningDot = 0.3f;

    [Export]
    public float DriftMinSideInertiaDot = 0.2f;

    [Export]
    public float DriftMaxSideInertiaDot = 1f;

    [Export]
    public float InertiaToVelocityRatio = 0.5f;

    // Boost after drifting. Something like All Stars Racing series.
    [Export]
    public bool BoostAtEndOfDrift = true;

    [Export]
    public float BoostAtEndOfDriftTimeLevel1 = 1; // TODO: Add more levels. Figure out the most clean way to handle boosts per level.
    private float DriftTimer = 0;

    const string InputActionName = "actionroll";

    public override void _Input(InputEvent @event)
    {
        if (CanDrift() && Player.PlayerInput.IsActionPressed(@event, InputActionName))
        {
            StartDrift();
        }
        else if (IsDrifting && Player.PlayerInput.IsActionReleased(@event, InputActionName))
        {
            StopDrift();
        }
    }

    public bool CanDrift()
    {
        var dot = Player.PlayerInput.LeftInput3D.Dot(Player.Basis.X);
        return (
            !IsDrifting
            && !Player.PlayerInput.TwodimensionalMode
            && Player.Grounded
            && (ActionBoost.IsBoosting || !RequireBoost)
            && !ActionGrind.IsGrinding()
            && Mathf.Abs(dot) > DriftMinInputDot
            && Mathf.IsZeroApprox(Player.PlayerInput.LeftInputTimedLock)
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsDrifting)
        {
            VisualDriftDir = Mathf.Lerp(VisualDriftDir, 0, VisualDirChangeSpeed * (float)delta);
            return;
        }

        if (
            Player.PlayerDamage.IsDead
            || (
                (!ActionBoost.IsBoosting && RequireBoost)
                || !Player.Grounded
                || ActionGrind.IsGrinding()
            )
        )
        {
            StopDrift();
            return;
        }

        // Drift strength
        float driftStrength = Player.PlayerInput.LeftRawInput.X;
        if (DriftDir > 0)
        {
            driftStrength = Mathf.Clamp(driftStrength, DriftMinTurningDot, 1);
        }
        else
        {
            driftStrength = Mathf.Clamp(driftStrength, -1, -DriftMinTurningDot);
        }

        Player.PlayerInput.LeftInputTimedLock = 0.1f;
        Player.PlayerInput.LeftInput3D =
            Player.Basis.X * DriftDir * Mathf.Abs(driftStrength)
            + -Player.Basis.Z * (1 - Mathf.Abs(driftStrength));

        VisualDriftDir = Mathf.Clamp(
            VisualDriftDir + (float)delta * DriftDir * VisualDirChangeSpeed,
            -1,
            1
        );

        // Drift inertia
        float inertiaStrength = Player.PlayerInput.LeftRawInput.X;
        if (DriftDir > 0)
        {
            inertiaStrength = Mathf.Clamp(
                inertiaStrength,
                -DriftMaxSideInertiaDot,
                -DriftMinSideInertiaDot
            );
        }
        else
        {
            inertiaStrength = Mathf.Clamp(inertiaStrength, DriftMaxSideInertiaDot, 1);
        }
        Vector3 targetInertia =
            Player.Basis.X * inertiaStrength * Player.VelocityXZ.Length() * InertiaToVelocityRatio;
        Vector3 inertiaDifference = targetInertia - Inertia;
        Inertia = targetInertia;
        Player.ConstantForce += inertiaDifference;

        // Misc
        if (
            DriftTimer < BoostAtEndOfDriftTimeLevel1
            && DriftTimer + (float)delta >= BoostAtEndOfDriftTimeLevel1
        )
        {
            // TODO: Effect here.
        }
        DriftTimer += (float)delta;
    }

    public void StartDrift()
    {
        var dot = Player.PlayerInput.LeftInput3D.Dot(Player.Basis.X);

        // Drift dir
        DriftDir = 0;
        if (dot > DriftMinInputDot)
        {
            DriftDir = 1f;
        }
        else if (dot < -DriftMinInputDot)
        {
            DriftDir = -1f;
        }
        if (DriftDir == 0)
            return;

        IsDrifting = true;
        Player.OnlyFastRotation = true;
        Player.AntiInertiaFactor = 2;

        Player.PlayerSkinController.ActionDrift = this;
        VisualDriftDir = 0;

        if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
        {
            PlayerCamera.Instances[Player.PlayerID].ForwardRepositionSpeed *= 5;
        }
        DriftSoundEffectPlayer.Stream = DriftSoundEffect;
        DriftSoundEffectPlayer.Play(0);

        if (ActionRoll.isRolling)
        {
            ActionRoll.StopRoll();
        }

        DriftTimer = 0;
    }

    public void StopDrift()
    {
        IsDrifting = false;
        Player.OnlyFastRotation = false;
        if (ActionBoost.IsBoosting)
        {
            Player.AntiInertiaFactor = ActionBoost.BoostingAntiInertiaFactor;
        }
        else
        {
            Player.AntiInertiaFactor = ActionBoost.InitialAntiInertiaFactor; // TODO: What if there's no boost?
        }
        DriftDir = 0;
        //VisualDriftDir = 0;
        if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
        {
            PlayerCamera.Instances[Player.PlayerID].ForwardRepositionSpeed /= 5;
        }

        if (DriftSoundEffectPlayer.Stream.ResourcePath == DriftSoundEffect.ResourcePath)
        {
            DriftSoundEffectPlayer.Stop();
        }

        Vector3 targetInertia = Vector3.Zero;
        Vector3 inertiaDifference = targetInertia - Inertia;
        Inertia = targetInertia;
        Player.ConstantForce += inertiaDifference;

        if (BoostAtEndOfDrift && DriftTimer > BoostAtEndOfDriftTimeLevel1)
        {
            ActionBoost.StartBoost();
        }
    }
}
