using System;
using System.Security.Cryptography.X509Certificates;
using Godot;

public partial class ActionDash : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public PlayerInput PlayerInput;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public ActionHoming ActionHoming;

    [Export]
    public ActionGrind ActionGrind;

    [Export]
    public ActionJump ActionJump;

    [Export]
    public ActionBoost ActionBoost;

    [Export]
    public ActionStomp ActionStomp;

    [Export]
    public ActionDrift ActionDrift;
    public int airDashCount = 0;

    [Export]
    public int MaxAllowedAirDashes = 1;

    [Export]
    public float DashSpeed = 50;

    [Export]
    public double DashCooldown = 0.3f;
    private double dashCooldownTimer = 0;

    [Export]
    public bool AllowMagnetDash = true;

    [Export]
    public bool AllowGroundDash = true;

    [Export]
    public bool DashUp = false;

    [Export]
    public GpuParticles3D Particles;

    [Export]
    public AudioStream groundDashSoundEffect;

    [Export]
    public AudioStream airDashSoundEffect;

    [Export]
    public AudioStreamPlayer3D audioPlayer;

    [Export]
    public string DashAnim = "";

    [Export]
    public string LastDashAnim = "";

    [Export]
    public string DashAction = "actiondash";

    [Export]
    public bool ExecutedByJump = false;

    [Export]
    public bool TriggersBoost = true;

    public override void _Ready()
    {
        if (Player == null)
        {
            Player = GetNodeOrNull<PlayerController>("../../..");
        }
        if (ActionBoost == null)
        {
            ActionBoost = Player.GetNodeOrNull<ActionBoost>("./PlayerControl/Actions/ActionBoost");
        }
        if (ActionStomp == null)
        {
            ActionStomp = Player.GetNodeOrNull<ActionStomp>("./PlayerControl/Actions/ActionStomp");
        }
        if (ActionDrift == null)
        {
            ActionDrift = Player.GetNodeOrNull<ActionDrift>("./PlayerControl/Actions/ActionDrift");
        }
    }

    public override void _Input(InputEvent @event)
    {
        bool canDash;
        if (ExecutedByJump)
        {
            canDash =
                !Player.Grounded
                && ActionJump.JumpCount > ActionJump.MaxJumpCount
                && (!ActionHoming.HomingAttackWithJumpButton || ActionHoming.MainTarget == null);
        }
        else
        {
            var isDrifting = ActionDrift != null && ActionDrift.IsDrifting;
            canDash =
                (AllowGroundDash || !Player.Grounded)
                && (!ActionRoll.IsSpinDashing)
                && !(
                    Player.Grounded && ActionRoll.isRolling
                ) /*&& !(isDrifting || PlayerInput.IsActionPressed("actionroll"))*/
            ;
        }
        if (canDash)
        {
            if (PlayerInput.IsActionJustPressed(DashAction))
            //if (PlayerInput.IsActionPressed(DashAction))
            {
                Dash();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Player.Grounded)
        {
            airDashCount = 0;
        }
        dashCooldownTimer -= delta;
        if (dashCooldownTimer < 0)
            dashCooldownTimer = 0;
    }

    public void Dash()
    {
        if (dashCooldownTimer <= 0 && (Player.Grounded || airDashCount < MaxAllowedAirDashes))
        {
            if (AllowMagnetDash)
            {
                ActionHoming.IsHoming = false;
            }
            else if (ActionHoming.IsHoming)
            {
                return;
            }
            else if (ActionStomp != null && ActionStomp.IsStomping)
            {
                ActionStomp.IsStomping = false;
            }
            if (!DashUp)
            {
                var thisDashSpeed = Player.VelocityXZ.Length();

                if (thisDashSpeed < DashSpeed * Player.SpeedMultiplier)
                    thisDashSpeed = DashSpeed * Player.SpeedMultiplier;

                if (
                    PlayerInput.LeftInput3D.Length() >= 0.5f
                    && !ActionGrind.IsGrinding()
                    && PlayerInput.LeftInputTimedLock <= 0
                ) // TODO: Configure dead zone
                {
                    Player.VelocityXZ = PlayerInput.LeftInput3D.Normalized() * thisDashSpeed;
                }
                else
                {
                    if (PlayerInput.TwodimensionalMode && !ActionGrind.IsGrinding())
                    {
                        Player.VelocityXZ =
                            Vector3Utils.ProjectOnPlane(
                                -Player.Basis.Z,
                                PlayerInput.Plane2DControlsX.Cross(PlayerInput.Plane2DControlsY)
                            ) * thisDashSpeed;
                    }
                    else
                    {
                        Player.VelocityXZ = -Player.Basis.Z * thisDashSpeed;
                    }
                }
                if (
                    ActionBoost != null
                    && !ActionBoost.AutoBoost
                    && ActionBoost.CanBoost()
                    && TriggersBoost
                )
                {
                    ActionBoost.StartBoost();
                    Player.VelocityXZ =
                        Player.VelocityXZ.Normalized()
                        * Mathf.Max(ActionBoost.MaxBoostSpeed, Player.VelocityXZ.Length());
                    Player.LockVelocity(0, 0);
                }
                Player.VelocityY = Vector3.Zero;
            }
            else
            {
                Player.VelocityY = Player.Basis.Y * DashSpeed;
            }
            airDashCount++;

            if (airDashCount < MaxAllowedAirDashes && DashAnim != "")
            {
                Player.PlayerSkinController.TravelAnimation(DashAnim);
            }
            else if (airDashCount == MaxAllowedAirDashes && LastDashAnim != "")
            {
                Player.PlayerSkinController.TravelAnimation(LastDashAnim);
            }
            if (!Player.Grounded)
            {
                ActionRoll.StopRoll();
            }

            if (ActionBoost == null || !ActionBoost.IsBoosting)
            {
                if (!Player.Grounded)
                {
                    audioPlayer.Stream = airDashSoundEffect;
                }
                else
                {
                    audioPlayer.Stream = groundDashSoundEffect;
                }
                audioPlayer.Play();
            }
            dashCooldownTimer = DashCooldown;
            if (Particles != null)
            {
                Particles.Restart();
            }
        }
    }
}
