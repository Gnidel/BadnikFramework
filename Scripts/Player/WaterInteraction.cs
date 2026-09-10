using System;
using Godot;

public partial class WaterInteraction : Node
{
    [Export]
    public float OxygenLimit = 30;

    [Export]
    public float WarningMusicTime = 11;

    [Export]
    public AudioStream DrowningWarningMusic;

    [Export]
    public AudioStream DrowningSoundEffect;

    [Export]
    public AudioStream SplashSoundEffect;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public float MinWaterRunVelocityXZ = 60;

    [Export]
    public float MaxWaterRunVelocityYAbs = 10;

    [Export]
    public float Slowdown = 0.1f;

    [Export]
    public GpuParticles3D[] CountdownParticles = new GpuParticles3D[0];

    [Export]
    public float Oxygen;
    public int WaterMouthCounter;
    public int WaterFeetCounter;
    private int LastCountdownEmited = 999;

    private PlayerController player;
    private ActionBoost ActionBoost;

    public override void _Ready()
    {
        player = GetNode<PlayerController>("../../");
        ActionBoost = player.GetNodeOrNull<ActionBoost>("./PlayerControl/Actions/ActionBoost");
        player.PlayerInventory.OnItemCollected += this.OnItemCollected;
    }

    private void OnItemCollected(string itemType, int newCount, int difference)
    {
        if (itemType == "bubble")
        {
            Oxygen = OxygenLimit;
            LastCountdownEmited = 999;

            if (MusicPlayerController.Instance.OverrideMusic == DrowningWarningMusic)
            {
                MusicPlayerController.Instance.OverrideMusic = null;
                MusicPlayerController.Instance.SwitchMusicToMain();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        DrowningSystem(delta);
        WaterRunSystem(delta);
    }

    private void DrowningSystem(double delta)
    {
        if (WaterMouthCounter <= 0)
        {
            WaterMouthCounter = 0;
            Oxygen = OxygenLimit;
            LastCountdownEmited = 999;

            if (MusicPlayerController.Instance.OverrideMusic == DrowningWarningMusic)
            {
                MusicPlayerController.Instance.OverrideMusic = null;
                MusicPlayerController.Instance.SwitchMusicToMain();
            }

            return;
        }

        Oxygen -= (float)delta;
        if (Oxygen < WarningMusicTime)
        {
            if (MusicPlayerController.Instance.OverrideMusic != DrowningWarningMusic)
            {
                MusicPlayerController.Instance.OverrideMusic = DrowningWarningMusic;
                MusicPlayerController.Instance.SwitchMusicToOverride();
            }
        }

        int countdownNumber = ((int)(Oxygen + 1) / 2);
        if (countdownNumber < CountdownParticles.Length && Oxygen > 0 && countdownNumber >= 0)
        {
            if (countdownNumber < LastCountdownEmited)
            {
                LastCountdownEmited = countdownNumber;
                CountdownParticles[countdownNumber].Emitting = true;
            }
        }

        if (Oxygen < 0)
        {
            var damage = this.GetNodeOrNull<PlayerDamage>("../Damage");
            if (damage != null && !damage.IsDead)
            {
                damage.DeathSound = DrowningSoundEffect;
                damage.Die();
            }
        }
    }

    public bool IsWaterRunning()
    {
        bool hasEnoughSpeed =
            player.LinearVelocity.ProjectOnPlane(player.Gravity.Normalized()).Length()
                >= MinWaterRunVelocityXZ
            && (
                player.LinearVelocity.Project(player.Gravity.Normalized()).Length()
                    < MaxWaterRunVelocityYAbs
                || player.Grounded
            );
        bool isBoosting = ActionBoost != null && ActionBoost.IsBoosting;
        return (WaterFeetCounter > 0 && WaterMouthCounter <= 0 && (hasEnoughSpeed || isBoosting));
    }

    public bool IsSteppingInWater()
    {
        return WaterFeetCounter > 0;
    }

    void WaterRunSystem(double delta)
    {
        bool isWaterRunning = IsWaterRunning();

        if (isWaterRunning)
        {
            player.Grounded = true;
            player.DisableGroundRayTime = 0.25f;
            player.GroundNormal = -player.Gravity.Normalized();

            if (player.VelocityYDirected <= 0)
            {
                player.LinearVelocity =
                    player.LinearVelocity.ProjectOnPlane(-player.Gravity.Normalized())
                    * (1f - Slowdown * (float)delta);
            }
        }
    }

    public void AddWaterMouthCounter(int diff)
    {
        WaterMouthCounter += diff;
        if (WaterMouthCounter < 0)
            WaterMouthCounter = 0;
    }

    public void AddWaterFeetCounter(int diff)
    {
        if ((WaterFeetCounter == 0 && diff > 0) || (diff < 0 && WaterFeetCounter - diff == 0))
        {
            AudioPlayer.Stream = SplashSoundEffect;
            AudioPlayer.Play(0);
        }
        WaterFeetCounter += diff;
        if (WaterFeetCounter < 0)
            WaterFeetCounter = 0;
    }
}
