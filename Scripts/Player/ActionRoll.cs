using System;
using Godot;

public partial class ActionRoll : Node, IActionUsingHitbox
{
    [Export]
    public PlayerController Player;

    [Export]
    public PlayerInput PlayerInput;

    [Export]
    public ActionGrind ActionGrind;

    [Export]
    public ActionBoost ActionBoost;

    [Export]
    public ActionDrift ActionDrift;

    [Export]
    public bool isRolling;

    [Export]
    public AudioStream rollSoundEffect;

    [Export]
    public AudioStream spinDashReleaseSoundEffect;

    [Export]
    public AudioStream dropDashChargeSoundEffect;

    [Export]
    public AudioStreamPlayer3D audioPlayer;

    [Export]
    public double LockRollTimer;

    //[Export] public Area3D Hitbox;

    public bool IsSpinDashing;
    public bool IsDropDashing;

    [Export]
    public float InitialSpinDashSpeed = 50;

    [Export]
    public float SpinDashSpeedPerCharge = 10;

    [Export]
    public int MaxCharges = 6;
    private int SpinDashCharges;
    private float CurrentChargeValue;

    [Export]
    public float ChargeVelocityRate = 10;

    [Export]
    public GpuParticles3D SpinDashParticles;

    [Export]
    public GpuParticles3D SpinDashChargeParticles;

    [Export]
    public bool CanManuallyRoll = true;

    [Export]
    public bool CanSpinDash = true;

    [Export]
    public bool CanDropDash = true;

    [Export]
    public bool AllowDuringBoost = true;

    private float HoldJumpTimer = 0;
    private bool DontAutoUnroll = false;

    [Export]
    public float DropDashHoldJumpTime = 0.5f;

    public override void _Process(double delta)
    {
        bool twoDimensionalRollInput =
            PlayerInput.TwodimensionalMode
            && PlayerInput.LeftRawInput.Y > 0.5f
            && Mathf.Abs(PlayerInput.LeftRawInput.X) < 0.5f;
        bool lockedByBoost = !AllowDuringBoost && ActionBoost != null && ActionBoost.IsBoosting;
        bool lockedByDrift = (
            ActionDrift != null && (ActionDrift.IsDrifting || (ActionDrift.CanDrift()))
        );

        DropDash(delta);
        // TODO: The input became really convoluted. Simplify this.
        if (
            !lockedByDrift
            && !lockedByBoost
            && CanManuallyRoll
            && (
                PlayerInput.IsActionJustPressed("actionroll")
                || twoDimensionalRollInput
                || (Player.Grounded && PlayerInput.IsActionJustPressed("actionstomp"))
            )
            && !isRolling
            && !ActionGrind.IsGrinding()
            && !(!Player.Grounded && Player.IsFlying)
        )
        {
            var playSound = !(
                Player.Grounded && Player.VelocityXZ.Length() <= 0.1f && !IsDropDashing
            );
            if (twoDimensionalRollInput)
            {
                DontAutoUnroll = true;
            }
            StartRoll(playSound);
        }
        if (
            CanManuallyRoll
            && PlayerInput.IsActionJustReleased("actionroll")
            && isRolling
            && !IsSpinDashing
        )
        {
            StopRoll();
        }

        LockRollTimer -= delta;
        if (LockRollTimer < 0)
            LockRollTimer = 0;

        SpinDash(delta);
        if (
            !DontAutoUnroll
            && isRolling
            && !IsSpinDashing
            && Player.Grounded
            && !PlayerInput.IsActionPressed("actionroll")
            && !twoDimensionalRollInput
            && LockRollTimer <= 0
        )
        {
            StopRoll();
        }
        if (
            !Player.Grounded
            || Player.VelocityXZ.Length() < 1
            || (ActionGrind != null && ActionGrind.IsGrinding())
        )
            DontAutoUnroll = false;

        //var hitboxActive = isRolling && (
        //    IsSpinDashing || !Player.Grounded || (Player.VelocityXZ.Length() > 0.1f)
        //    );
        //Hitbox.ProcessMode = hitboxActive ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        //Hitbox.Visible = hitboxActive;

        if (isRolling)
        {
            if ((Player.LinearVelocity.Normalized()).Dot(Player.Gravity.Normalized()) >= 0)
            {
                Player.SlopeGravityFactor = 2f;
            }
            else
            {
                Player.SlopeGravityFactor = 0.3f;
            }
        }

        if (Player.PlayerSkinController.SpinBall != null)
        {
            Player.PlayerSkinController.SpinBall.Visible = IsDropDashing;
        }
        if (Player.PlayerSkinController.SpinDashBall != null)
        {
            Player.PlayerSkinController.SpinDashBall.Visible = IsSpinDashing;
        }
    }

    private void SpinDash(double delta)
    {
        if (CanSpinDash && isRolling && Player.Grounded)
        {
            bool twoDimensionalRollInput =
                PlayerInput.TwodimensionalMode
                && PlayerInput.LeftRawInput.Y > 0.5f
                && Mathf.Abs(PlayerInput.LeftRawInput.X) < 0.5f; //  TODO: Redundancy
            bool rollInput = PlayerInput.IsActionPressed("actionroll") || twoDimensionalRollInput;

            bool buttonPressed =
                PlayerInput.IsActionJustPressed("actiondash")
                || (
                    PlayerInput.TwodimensionalMode && PlayerInput.IsActionJustPressed("actionjump")
                );
            if (
                ((IsSpinDashing || rollInput) && buttonPressed)
                || PlayerInput.IsActionJustPressed("actionstomp")
            )
            {
                audioPlayer.Stream = rollSoundEffect;
                audioPlayer.Play();
                if (!IsSpinDashing)
                {
                    IsSpinDashing = true;
                    CurrentChargeValue = InitialSpinDashSpeed;
                    SpinDashCharges = 1;
                    if (ActionBoost != null && ActionBoost.IsBoosting)
                    {
                        ActionBoost.StopBoost();
                    }
                }
                else
                {
                    CurrentChargeValue = Mathf.Min(
                        CurrentChargeValue + SpinDashSpeedPerCharge,
                        MaxCharges * SpinDashSpeedPerCharge + InitialSpinDashSpeed
                    );
                    //if (SpinDashCharges < MaxCharges)
                    //{
                    //    CurrentChargeValue += SpinDashSpeedPerCharge;
                    //    SpinDashCharges++;
                    //}
                    SpinDashChargeParticles.Emitting = false;
                    SpinDashChargeParticles.Restart();
                }
            }

            if (IsSpinDashing)
            {
                if (
                    PlayerInput.IsActionJustReleased("actionroll")
                    || PlayerInput.IsActionJustReleased("actionstomp")
                    || (PlayerInput.PrevLeftRawInput.Y > 0.5f && PlayerInput.LeftRawInput.Y < 0.2f)
                )
                {
                    CurrentChargeValue *= Player.SpeedMultiplier;
                    // Blast away!
                    audioPlayer.Stream = spinDashReleaseSoundEffect;
                    audioPlayer.Play();

                    IsSpinDashing = false;
                    Player.DisableNoInputDecceleration = true;
                    LockRollTimer = 0.3f;
                    if (PlayerInput.TwodimensionalMode)
                    {
                        // Player.LinearVelocity = Vector3Utils.ProjectOnPlane(-Player.Basis.Z, PlayerInput. * CurrentChargeValue;
                        Player.VelocityXZ =
                            Vector3Utils
                                .ProjectOnPlane(
                                    -Player.Basis.Z,
                                    PlayerInput.Plane2DControlsX.Cross(PlayerInput.Plane2DControlsY)
                                )
                                .Normalized() * CurrentChargeValue;
                    }
                    else
                    {
                        Player.LinearVelocity = -Player.Basis.Z * CurrentChargeValue;
                    }
                    DontAutoUnroll = true;
                    if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
                    {
                        var camera = PlayerCamera.Instances[Player.PlayerID];
                        camera.SetShake();
                    }
                }
                else
                {
                    Player.VelocityXZ = Player.VelocityXZ.Lerp(
                        Vector3.Zero,
                        (float)delta * ChargeVelocityRate
                    );
                    CurrentChargeValue += (float)delta * SpinDashSpeedPerCharge * MaxCharges;
                }
            }
        }
        else if (!Player.Grounded)
        {
            IsSpinDashing = false;
        }

        SpinDashParticles.Emitting = IsSpinDashing;

        SpinDashParticles.SpeedScale = IsSpinDashing ? 1 : 2;
    }

    private void DropDash(double delta)
    {
        if (!CanDropDash || Player.IsFlying)
            return;
        if (PlayerInput.IsActionPressed("actionjump"))
        {
            HoldJumpTimer += (float)delta;
        }
        else
        {
            HoldJumpTimer = 0;
        }
        if (IsDropDashing && PlayerInput.IsActionJustReleased("actionjump"))
        {
            IsDropDashing = false;
        }

        if (!Player.Grounded && !IsDropDashing)
        {
            if (
                (!isRolling && PlayerInput.IsActionJustPressed("actionroll"))
                || (isRolling && HoldJumpTimer > DropDashHoldJumpTime)
                    && PlayerInput.IsActionPressed("actionjump")
            )
            {
                IsDropDashing = true;
                isRolling = true;
                CurrentChargeValue = InitialSpinDashSpeed * Player.SpeedMultiplier;
                audioPlayer.Stream = dropDashChargeSoundEffect;
                audioPlayer.Play(0);
            }
        }

        if (IsDropDashing)
        {
            if (Player.Grounded)
            {
                IsDropDashing = false;
                isRolling = true;
                Player.DisableNoInputDecceleration = true;
                audioPlayer.Stream = spinDashReleaseSoundEffect;
                audioPlayer.Play();
                // Blast away!
                if (PlayerInput.TwodimensionalMode)
                {
                    Player.VelocityXZ =
                        Vector3Utils
                            .ProjectOnPlane(
                                -Player.Basis.Z,
                                PlayerInput.Plane2DControlsX.Cross(PlayerInput.Plane2DControlsY)
                            )
                            .Normalized() * CurrentChargeValue;
                }
                else
                {
                    Player.LinearVelocity = -Player.Basis.Z * CurrentChargeValue;
                }

                DontAutoUnroll = true;
                if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
                {
                    var camera = PlayerCamera.Instances[Player.PlayerID];
                    camera.SetShake();
                }
            }
            else
            {
                CurrentChargeValue = Mathf.Max(CurrentChargeValue, Player.LinearVelocity.Length());
            }
        }
    }

    public void StartRoll(bool playSound)
    {
        isRolling = true;
        if (playSound)
        {
            audioPlayer.Stream = rollSoundEffect;
            audioPlayer.Play();
        }
        Player.DisableNoInputDecceleration = true;
    }

    public void StopRoll()
    {
        isRolling = false;
        IsSpinDashing = false;
        IsDropDashing = false;
        Player.SlopeGravityFactor = 0.3f;
        Player.DisableNoInputDecceleration = false;
    }

    public void SetRoll(bool nextRolling)
    {
        if (this.isRolling && !nextRolling)
        {
            StopRoll();
        }
        else if (!isRolling && nextRolling)
        {
            StartRoll(false);
        }
    }

    public bool IsHitboxEnabled()
    {
        return isRolling
            && (IsSpinDashing || !Player.Grounded || (Player.VelocityXZ.Length() > 0.1f));
    }
}
