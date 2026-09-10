using Godot;

public partial class StunLaser : Laser
{
    [Signal]
    public delegate void OnLaserHitEventHandler(double laserMovementPauseTime);

    [Export]
    public Node3D KnockbackOrigin;

    [Export]
    public float KnockbackVelocity = 50f;

    [Export]
    public double LockInputTime = 2f;

    [Export]
    public double LaserMovementPauseTime = 5f;

    private ulong lastHitTime;

    public override void _Ready()
    {
        base._Ready();
        KnockbackOrigin ??= this;
        Hitbox.CanDamagePlayer = false;
        Hitbox.AreaEntered += OnAreaEntered;
    }

    private void OnAreaEntered(Area3D area)
    {
        var hurtbox = area as Hurtbox;
        if (hurtbox == null)
            return;

        var player = FindPlayer(hurtbox);
        if (player == null || player.PlayerDamage.IsDead)
            return;

        var now = Time.GetTicksMsec();
        if (now < lastHitTime + 250)
            return;

        lastHitTime = now;

        var knockbackDirection = player.GlobalPosition - KnockbackOrigin.GlobalPosition;
        knockbackDirection.Y = 0;
        if (knockbackDirection.LengthSquared() < 0.0001f)
            return;

        player.LinearVelocity = knockbackDirection.Normalized() * KnockbackVelocity;
        player.Grounded = false;
        player.noGroundTimer = 0.1f;
        player.PlayerInput.LeftInput3D = Vector3.Zero;
        player.PlayerInput.LeftInputTimedLock = Mathf.Max(
            player.PlayerInput.LeftInputTimedLock,
            LockInputTime
        );
        player.PlayerSkinController.TravelAnimation("Damage", true);

        EmitSignal(SignalName.OnLaserHit, LaserMovementPauseTime);
    }

    private static PlayerController FindPlayer(Node node)
    {
        while (node != null)
        {
            if (node is PlayerController player)
                return player;
            node = node.GetParent();
        }

        return null;
    }
}