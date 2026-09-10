using System;
using System.Linq;
using Godot;

public partial class ActionBoost : Node, IActionUsingHitbox
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionDash ActionDash;

    [Export]
    public ActionDrift ActionDrift;

    [Export]
    public ActionSuperTransformation ActionSuperTransformation;

    [Export]
    public float BoostEndSlowRatio = 0.2f;

    [Export]
    public float StopBoostVelocityXZ = 10f;

    [Export]
    public float BoostingAntiInertiaFactor = 1f;

    [Export]
    public GpuParticles3D Particles;

    [Export]
    public GpuParticles3D SuperParticles;

    [Export]
    public Node3D Trail;

    [Export]
    public Node3D SuperTrail;

    [Export]
    public GpuParticles3D ShockwaveParticles;

    [Export]
    public bool RequireSuperTransformation = true;

    [Export]
    public AudioStreamPlayer3D AudioStreamPlayer;

    [Export]
    public AudioStream SoundEffect;

    [Export]
    public bool ShowBoostMeter = true;

    [Export]
    public AudioStreamPlayer3D ContinousAudioStreamPlayer;

    [Export]
    public AudioStream ContinousSoundEffect;

    [Export]
    public float MinBoostSpeed = 100f;

    [Export]
    public bool AutoBoost = false;

    [Export]
    public bool AutoBoostStop = false;

    [Export]
    public float AutoBoostSpeed = 100f;

    public float MaxBoostSpeed;
    public bool IsBoosting = false;
    public float InitialAntiInertiaFactor;
    private bool GroundBoost = false;

    public float Fuel = 1f;

    [Export]
    public float StartFuelConsumptionConsumption = 0.1f;

    [Export]
    public float FuelConsumptionPerSecond = 0.1f;

    [Export]
    public float FuelRegenerationPerSecond = 0.5f;

    [Export]
    public float FuelRegenerationDelay = 1f;
    private float FuelRegenerationTimer = 0;

    [Export]
    public string[] FuelItems = { "rings" };

    [Export]
    public float FuelPerItem = 0.1f;

    public override void _Ready()
    {
        if (Player == null)
        {
            Player = GetNodeOrNull<PlayerController>("../../..");
        }
        if (ActionDash == null)
        {
            ActionDash = Player.GetNodeOrNull<ActionDash>("./PlayerControl/Actions/ActionDash");
        }
        if (ActionDrift == null)
        {
            ActionDrift = Player.GetNodeOrNull<ActionDrift>("./PlayerControl/Actions/ActionDrift");
        }
        if (ActionSuperTransformation == null)
        {
            ActionSuperTransformation = Player.GetNodeOrNull<ActionSuperTransformation>(
                "./PlayerControl/Actions/ActionSuperTransformation"
            );
        }

        InitialAntiInertiaFactor = Player.AntiInertiaFactor;
        Particles.Emitting = false;
        if (Trail != null)
            Trail.Visible = Particles.Emitting;
        if (SuperTrail != null)
            SuperTrail.Visible = SuperParticles.Emitting;

        Player.PlayerInventory.OnItemCollected += this.OnItemCollected;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ActionSuperTransformation.IsSuper)
        {
            Fuel = 1;
        }
        if (IsBoosting)
        {
            if (
                !(AutoBoost || AutoBoostStop)
                && !Player.PlayerInput.IsActionPressed(ActionDash.DashAction)
            )
            {
                StopBoost();
                return;
            }

            if (
                Player.VelocityXZ.Length() < StopBoostVelocityXZ * Player.SpeedMultiplier
                || Player.VelocityXZ.Length() < Player.VelocityY.Length()
                || ((AutoBoost || AutoBoostStop) && Player.PlayerInput.LeftInput3D.Length() < 0.5f)
            )
            {
                StopBoost();
                return;
            }
            if (RequireSuperTransformation && !ActionSuperTransformation.IsSuper)
            {
                StopBoost();
                return;
            }
            if (Fuel <= 0)
            {
                Fuel = 0;
                StopBoost();
                return;
            }
        }
        if (!IsBoosting)
        {
            if (
                AutoBoost
                && Player.VelocityXZ.Length() > AutoBoostSpeed * Player.SpeedMultiplier
                && Player.Grounded
            )
            {
                StartBoost();
            }
        }
        if (!IsBoosting)
        {
            FuelRegenerationTimer += (float)delta;
            if (FuelRegenerationTimer > FuelRegenerationDelay)
            {
                Fuel = Mathf.Clamp(Fuel + FuelRegenerationPerSecond * (float)delta, 0, 1);
            }
            return;
        }
        FuelRegenerationTimer = 0;
        MaxBoostSpeed = Mathf.Max(MaxBoostSpeed, Player.VelocityXZ.Length());
        if (Player.Grounded)
        {
            if (Player.GetCollidingBodies().Count <= 1)
            {
                Player.VelocityXZ = Player.VelocityXZ.Normalized() * MaxBoostSpeed;
            }
            GroundBoost = true;
        } //else if (GroundBoost && !AutoBoost)
        //{
        //StopBoost();
        //}
        if (!ActionSuperTransformation.IsSuper)
        {
            Fuel = Mathf.Clamp(Fuel - FuelConsumptionPerSecond * (float)delta, 0, 1);
        }
    }

    // Called from ActionDash
    public void StartBoost()
    {
        if (!CanBoost())
            return;
        IsBoosting = true;
        MaxBoostSpeed = MinBoostSpeed * Player.SpeedMultiplier;
        Player.AntiInertiaFactor = BoostingAntiInertiaFactor;

        if (ActionSuperTransformation.IsSuper)
        {
            SuperParticles.Emitting = true;
            Particles.Emitting = false;
        }
        else
        {
            SuperParticles.Emitting = false;
            Particles.Emitting = true;
        }
        ShockwaveParticles.Restart();
        ShockwaveParticles.Emitting = true;

        AudioStreamPlayer.Stream = this.SoundEffect;
        AudioStreamPlayer.Play(0);

        ContinousAudioStreamPlayer.Stream = this.ContinousSoundEffect;
        ContinousAudioStreamPlayer.Play(0);

        GroundBoost = Player.Grounded;
        if (Trail != null)
            Trail.Visible = Particles.Emitting;
        if (SuperTrail != null)
            SuperTrail.Visible = SuperParticles.Emitting;

        Fuel -= StartFuelConsumptionConsumption;
        if (Fuel < 0)
            Fuel = 0;
        FuelRegenerationTimer = 0;

        if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
        {
            var camera = PlayerCamera.Instances[Player.PlayerID];
            camera.SetShake();
        }
    }

    public bool CanBoost()
    {
        if (this.ProcessMode == ProcessModeEnum.Disabled)
            return false;
        if (RequireSuperTransformation && !ActionSuperTransformation.IsSuper)
            return false;
        if (IsBoosting)
            return false;
        if (Fuel <= 0)
            return false;
        return true;
    }

    public void StopBoost()
    {
        if (this.ProcessMode == ProcessModeEnum.Disabled)
            return;
        if (!IsBoosting)
            return;
        IsBoosting = false;
        Player.VelocityXZ *= (1f - BoostEndSlowRatio);
        if (!ActionDrift.IsDrifting)
        {
            Player.AntiInertiaFactor = InitialAntiInertiaFactor;
        }
        MaxBoostSpeed = 0;
        Particles.Emitting = false;
        SuperParticles.Emitting = false;

        if (ContinousAudioStreamPlayer.Stream == this.ContinousSoundEffect)
        {
            ContinousAudioStreamPlayer.Stop();
        }
        if (Trail != null)
            Trail.Visible = Particles.Emitting;
        if (SuperTrail != null)
            SuperTrail.Visible = SuperParticles.Emitting;
    }

    public bool IsHitboxEnabled()
    {
        return IsBoosting;
    }

    private void OnItemCollected(string itemType, int newCount, int difference)
    {
        if (FuelItems.Contains(itemType))
        {
            Fuel = Mathf.Clamp(Fuel + difference * FuelPerItem, 0, 1);
        }
    }
}
