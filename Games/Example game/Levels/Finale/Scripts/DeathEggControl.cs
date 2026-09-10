using System;
using System.Linq;
using Godot;

public partial class DeathEggControl : Node
{
    [Export]
    public Node3D DeathEggRoot;

    [Export]
    public int MaxHP = 20;

    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public Node3D[] Lasers;

    [Export]
    public PackedScene WeakPointPrefab;

    [Export]
    public Vector3 CurrentTarget;

    [Export]
    public float CurrentTargetRepositionSpeed = 2f;

    private double laserMovementPauseTimer;

    [Export]
    Node3D WeakPointContainer;

    [Export]
    float DistanceFromCenter = 56;

    [Export]
    public float RotateAtDistanceFromPlayer = 100;

    [Export]
    public float RotationSpeed = 1f;

    public override void _Ready()
    {
        foreach (var laserMount in Lasers)
        {
            var stunLaser = laserMount.GetNodeOrNull<StunLaser>("[Interactable] Stun Laser");
            if (stunLaser != null)
            {
                stunLaser.OnLaserHit += PauseLaserMovement;
            }
        }

        SpawnWeakPoints(MaxHP);
        EnemyControl.MaxHP = MaxHP;
        EnemyControl.HealthBar.SetMaxHP(MaxHP);
        EnemyControl.HP = MaxHP;
        EnemyControl.HealthBar.SetHP(MaxHP);

        CurrentTarget = PlayerController.Instances.First().GlobalPosition + Vector3.Up * 200f;
    }

    void SpawnWeakPoints(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var newWeakPoint = WeakPointPrefab.Instantiate<DeathEggWeakSpot>();
            WeakPointContainer.AddChild(newWeakPoint);

            newWeakPoint.Position = new Vector3(0, DistanceFromCenter, 0);

            newWeakPoint.GlobalRotationDegrees = new Vector3(90, (float)i / (float)count * 360f, 0);
            //newWeakPoint.GlobalPosition = Quaternion.FromEuler(new Vector3(0, (float)i / (float)count * 360, 0)) * newWeakPoint.GlobalPosition;
            newWeakPoint.Position =
                new Vector3(
                    Mathf.Sin((float)i / (float)count * 2 * Mathf.Pi),
                    Mathf.Cos((float)i / (float)count * 2 * Mathf.Pi),
                    0
                ) * DistanceFromCenter;
            newWeakPoint.EnemyControl = EnemyControl;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        laserMovementPauseTimer = Mathf.Max(0, laserMovementPauseTimer - delta);

        PlayerController TargetPlayer = null;
        foreach (var player in PlayerController.Instances)
        {
            if (player.NpcPartnerControl != null && player.NpcPartnerControl.IsNpc)
            {
                continue;
            }
            TargetPlayer = player;
            break;
        }
        if (TargetPlayer == null)
            return;



        // Rotate the model around world Y while preserving its imported orientation.
        var offset = Vector3.Zero;
        foreach (var player in PlayerController.Instances)
        {
            if (player.NpcPartnerControl != null && player.NpcPartnerControl.IsNpc)
            {
                continue;
            }
            var thisPlayerOffset = player.GlobalPosition - DeathEggRoot.GlobalPosition;
            thisPlayerOffset.Y = 0;
            if (thisPlayerOffset.Length() > offset.Length())
            {
                offset = thisPlayerOffset;
            }
        }

        if (offset.Length() > RotateAtDistanceFromPlayer)
        {
            var currentForward = DeathEggRoot.GlobalBasis.Y;
            currentForward.Y = 0;
            currentForward = currentForward.Normalized();

            var targetDirection = offset.Normalized();
            var angleToTarget = Mathf.Atan2(
                currentForward.Cross(targetDirection).Y,
                currentForward.Dot(targetDirection)
            );
            var rotationStep = Mathf.Clamp(
                angleToTarget,
                -RotationSpeed * (float)delta,
                RotationSpeed * (float)delta
            );
            DeathEggRoot.GlobalBasis = new Basis(Vector3.Up, rotationStep) * DeathEggRoot.GlobalBasis;
        }

        var playerOffset = TargetPlayer.GlobalPosition - DeathEggRoot.GlobalPosition;
        playerOffset.Y = 0;
        var forward = DeathEggRoot.GlobalBasis.Y;
        forward.Y = 0;
        forward = forward.Normalized();
        bool laserActive =
            playerOffset.Length() > RotateAtDistanceFromPlayer
            && playerOffset.Normalized().Dot(forward) > 0.5f;

        if (laserMovementPauseTimer <= 0)
        {
            CurrentTarget = CurrentTarget.Lerp(
                TargetPlayer.GlobalPosition + TargetPlayer.LinearVelocity.Normalized() * 10f,
                CurrentTargetRepositionSpeed * (float)delta
            );
        }
        foreach (var laser in Lasers)
        {
            if (laserMovementPauseTimer <= 0)
            {
                laser.LookAt(CurrentTarget);
            }
            laser.ProcessMode = laserActive ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            laser.Visible = laserActive;
        }

    }

    public void PauseLaserMovement(double duration)
    {
        laserMovementPauseTimer = Mathf.Max(laserMovementPauseTimer, duration);
    }
}
