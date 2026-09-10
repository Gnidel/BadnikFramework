using System;
using System.Threading.Tasks;
using Godot;

public partial class ActionStomp : Node, IActionUsingHitbox
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public ActionHoming ActionHoming;

    [Export]
    public float InitialDownVelocity = 100;

    [Export]
    public float BounceVelocity = 45;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream StompStartSound;

    [Export]
    public AudioStream StompImpactSound;

    [Export]
    public AudioStream BounceSound;

    [Export]
    public bool CanBounce = true;

    [Export]
    public Area3D ShockwaveHitbox;

    [Export]
    public GpuParticles3D ShockwaveParticles;
    public bool IsStomping = false;
    public bool LockedAction = false;

    public override void _Ready()
    {
        if (Player == null)
        {
            Player = GetNodeOrNull<PlayerController>("../../..");
        }

        if (ActionRoll == null)
        {
            ActionRoll = Player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        }

        if (ActionHoming == null)
        {
            ActionHoming = Player.GetNodeOrNull<ActionHoming>(
                "./PlayerControl/Actions/ActionHoming"
            );
        }

        if (ShockwaveHitbox != null)
        {
            ShockwaveHitbox.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (
            !Player.Grounded
            && !IsStomping
            && Player.PlayerInput.IsActionPressed(@event, "actionstomp")
            && !ActionHoming.IsHoming
            && !LockedAction
        )
        {
            StartDropping();
        }
        else if (
            IsStomping && Player.PlayerInput.IsActionPressed(@event, "actionstomp")
            || Player.PlayerInput.IsActionPressed(@event, "actionroll")
        )
        {
            CancelStomp();
        }
    }

    void StartDropping()
    {
        Player.LinearVelocity = Player.Gravity.Normalized() * InitialDownVelocity;
        IsStomping = true;
        //Player.PlayerInput.LockedInput = true;
        ActionRoll.isRolling = false;
        Player.PlayerSkinController.TravelAnimation("Stomp");
        AudioPlayer.Stream = StompStartSound;
        AudioPlayer.Play(0);
    }

    void LandStopStomp()
    {
        IsStomping = false;
        //Player.PlayerInput.LockedInput = false;
        Player.PlayerSkinController.TravelAnimation("Squat", true);
        ActionRoll.isRolling = false;
        AudioPlayer.Stream = StompImpactSound;
        AudioPlayer.Play(0);
    }

    public void CancelStomp()
    {
        IsStomping = false;
        if (Player.Grounded)
        {
            Player.PlayerSkinController.TravelAnimation("Idle", true);
        }
        else
        {
            Player.PlayerSkinController.TravelAnimation("Air", true);
        }
    }

    async void LandBounceAsync()
    {
        IsStomping = false;
        //Player.PlayerInput.LockedInput = false;
        Player.PlayerSkinController.TravelAnimation("Air", true);

        AudioPlayer.Stream = BounceSound;
        AudioPlayer.Play(0);

        await ToSignal(GetTree(), "physics_frame"); // Give a frame to let other actions refresh their states.
        await ToSignal(GetTree(), "physics_frame");
        Player.LinearVelocity = Player.GroundNormal.Normalized() * BounceVelocity;
        ActionRoll.isRolling = true;
        Player.Grounded = false;
        Player.DisableGroundRayTime = 0.2f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsStomping)
            return;
        if (Player.Grounded)
        {
            var actionGrind = Player.GetNodeOrNull<ActionGrind>(
                "./PlayerControl/Actions/ActionGrind"
            );
            bool isGrinding = actionGrind != null && actionGrind.IsGrinding();
            if (
                !Player.PlayerInput.IsActionPressed("actionstomp", true)
                || !CanBounce
                || isGrinding
            )
            {
                LandStopStomp();
            }
            else
            {
                LandBounceAsync();
            }
            if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
            {
                var camera = PlayerCamera.Instances[Player.PlayerID];
                camera.SetShake();
            }
            SpawnShockwave();
            return;
        }
        Player.PlayerSkinController.TravelAnimation("Stomp");
        Player.LinearVelocity = Player.Gravity.Normalized() * InitialDownVelocity;
    }

    async void SpawnShockwave()
    {
        ShockwaveHitbox.ProcessMode = ProcessModeEnum.Inherit;
        ShockwaveParticles.Restart();
        ShockwaveParticles.Emitting = true;
        await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        ShockwaveHitbox.ProcessMode = ProcessModeEnum.Disabled;
    }

    public bool IsHitboxEnabled()
    {
        return IsStomping;
    }
}
