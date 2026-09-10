using System;
using Godot;

public partial class PlayerDamage : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public PlayerInventory PlayerInventory;

    [Export]
    public ActionHoming ActionHoming;

    [Export]
    public ActionRoll ActionRoll;

    //[Export] public Node3D PlayerModel;
    [Export]
    public PackedScene RingsPrefab;

    [Export]
    public float Radius = 1f;

    [Export]
    public float EjectVelocity = 2f;

    [Export]
    public AudioStream RingsSpreadSound;

    [Export]
    public AudioStream DeathSound;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public bool NpcProtection = true;

    [Export]
    public float DamageSpeedLossMultiplier = 1f;

    [Export]
    public double InvincibilityTime = 5f;

    [Export]
    public bool BlinkOnInvincibility;
    public double invincibilityTimer = 0;
    public bool IsInvincible
    {
        get { return invincibilityTimer > 0; }
    }
    public bool IsDamaged;
    public bool IsDead;

    public override void _Process(double delta)
    {
        invincibilityTimer -= delta;
        if (invincibilityTimer <= 0)
        {
            invincibilityTimer = 0;
            Player.PlayerSkinController.Visible = true;
        }
        else if (BlinkOnInvincibility)
        {
            Player.PlayerSkinController.Visible = !Player.PlayerSkinController.Visible;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            //Player.Enabled = false;
            Player.PlayerInput.LockedInput = true;
            Player.PlayerInput.LeftInput3D = Vector3.Zero;
            Player.VelocityXZ = Player.VelocityXZ.Lerp(Vector3.Zero, (float)delta * 5);
        }
    }

    public void OnHitboxEnter(Hitbox hitbox, Hurtbox hurtbox)
    {
        if (IsDead)
            return;
        if (
            hitbox.IsInstaKill
            || (hitbox.CanDamagePlayer && (hitbox.CanDamageRollingPlayer || !ActionRoll.isRolling))
                && invincibilityTimer <= 0
        )
        {
            if (this.ProcessMode == ProcessModeEnum.Disabled && !hitbox.IsInstaKill)
                return;

            var ringsCount = PlayerInventory.GetItemCount("rings");
            if (
                (!hitbox.IsInstaKill && ringsCount > 0)
                || (Player.NpcPartnerControl.IsNpc && NpcProtection)
            )
            {
                IsDamaged = true;
                RestoreNotDamaged();
                invincibilityTimer = InvincibilityTime;
                if (ActionHoming.IsHoming)
                {
                    Player.LinearVelocity = Vector3.Zero;
                }
                ActionHoming.IsHoming = false;
                ActionRoll.isRolling = false;

                Player.LinearVelocity *= DamageSpeedLossMultiplier;

                if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
                {
                    var camera = PlayerCamera.Instances[Player.PlayerID];
                    camera.SetShake();
                }
            }
            else
            {
                Die();
            }
            if (!Player.NpcPartnerControl.IsNpc || !NpcProtection)
            {
                PlayerInventory.SetItemCount("rings", 0);
                SpawnRings(ringsCount);
                Engine.TimeScale = 0.1f;
                RestoreSlowmo();
            }
        }
    }

    private void SpawnRings(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var sin = Mathf.Sin((float)i / (float)count * 2 * Mathf.Pi);
            var cos = Mathf.Cos((float)i / (float)count * 2 * Mathf.Pi);
            var ringsInstance = RingsPrefab.Instantiate<RigidBody3D>();
            this.GetParent().GetParent().GetParent().AddChild(ringsInstance);

            // TODO: Rings in 2D

            var ringDirVector = new Vector3(sin, 0, cos);
            ringDirVector =
                Player.Basis.GetRotationQuaternion()
                * new Quaternion(new Vector3(0, 1, 0), Mathf.Pi / 4)
                * ringDirVector;

            ringsInstance.GlobalPosition =
                Player.GlobalPosition + ringDirVector * Radius + Player.GroundNormal.Normalized();
            ringsInstance.LinearVelocity = ringDirVector * EjectVelocity;
            ringsInstance.AngularVelocity = new Vector3(sin, 0, cos) * EjectVelocity * Mathf.Pi;
            ringsInstance.GravityScale = 0;
            ringsInstance.ConstantForce = Player.Gravity;

            AudioPlayer.Stream = RingsSpreadSound;
            AudioPlayer.Play();
        }
    }

    public void Die()
    {
        IsDead = true;
        AudioPlayer.Stream = DeathSound;
        AudioPlayer.Play();

        if (StageData.Instance.Style == StageData.StageStyle.ActionStage)
        {
            CheckpointSystem.DeathCount++;

            if (AlivePlayersCount() == 0 && !CheckpointSystem.ALWAYS_RESPAWN_NO_RELOAD)
            {
                StageData.Instance.CountTime = false;
                CheckpointSystem.CheckpointTime = StageData.Instance.ElapsedTime;
                PauseMenu.Instance.Restart();
            }
        }
    }

    public static int AlivePlayersCount()
    {
        int alivePlayersCount = 0;
        foreach (var player in PlayerController.Instances)
        {
            if (!player.NpcPartnerControl.IsNpc && !player.PlayerDamage.IsDead)
            {
                alivePlayersCount++;
            }
        }
        return alivePlayersCount;
    }

    private async void RestoreSlowmo()
    {
        await ToSignal(GetTree().CreateTimer(0.05f), "timeout");
        Engine.TimeScale = 1;
    }

    private async void RestoreNotDamaged()
    {
        await ToSignal(GetTree().CreateTimer(0.05f), "timeout");
        IsDamaged = false;
    }

    public void Resurrect()
    {
        IsDead = false;
        Player.PlayerSkinController.TravelAnimation("Idle", true);
        Player.LinearVelocity = Vector3.Zero;

        Player.PlayerInput.LockedInput = false;
        Player.PlayerInput.LeftInput3D = Vector3.Zero;
    }
}
