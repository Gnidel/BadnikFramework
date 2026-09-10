using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ActionGun : Node
{
    [Export]
    public float Range = 200f;

    [Export]
    public GunStyle Style = GunStyle.Adventure;

    [Export]
    public double TargettingCooldown = 0.1f;
    private double targettingTimer = 0;

    [Export]
    public double FireCooldown = 0.1f;
    private double fireTimer = 0;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream TargettingBeepSound;

    [Export]
    public AudioStream GunFireSound;

    [Export]
    public Vector3 MuzzleOffset = new Vector3(0, 1f, -2);

    //[Export] public PackedScene ExplosionPrefab;
    [Export]
    public PackedScene BulletPrefab;

    [Export]
    public PackedScene ComboTargetPrefab;

    public PlayerController Player;
    private ActionRoll ActionRoll;
    public List<HomingTarget> CurrentTargets = new List<HomingTarget>();
    private List<ComboTarget> CurrentComboTargets = new List<ComboTarget>();
    public GunStateEnum GunState;

    public enum GunStyle
    {
        Adventure,
        Shadow,
    }

    public enum GunStateEnum
    {
        IDLE,
        TARGETING,
        FIRING,
    }

    public override void _Ready()
    {
        Player = GetNodeOrNull<PlayerController>("../../..");
        ActionRoll = this.GetNodeOrNull<ActionRoll>("../ActionRoll");
    }

    public override void _Input(InputEvent @event)
    {
        const string INPUT_ACTION = "actionhoming";
        if (
            Player.PlayerInput.IsActionPressed(@event, INPUT_ACTION)
            && GunState == GunStateEnum.IDLE
        )
        {
            GunState = GunStateEnum.TARGETING;
            targettingTimer = TargettingCooldown;
        }

        if (Style == GunStyle.Adventure)
        {
            if (
                Player.PlayerInput.IsActionReleased(@event, INPUT_ACTION)
                && GunState == GunStateEnum.TARGETING
            )
            {
                if (CurrentTargets.Count == 0)
                {
                    GunState = GunStateEnum.IDLE;
                }
                else
                {
                    GunState = GunStateEnum.FIRING;
                    fireTimer = 0;
                    if (ActionRoll != null)
                    {
                        ActionRoll.isRolling = false;
                    }
                }
            }
        }
        else if (
            Style == GunStyle.Shadow
            && Player.PlayerInput.IsActionReleased(@event, INPUT_ACTION)
        )
        {
            GunState = GunStateEnum.IDLE;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (GunState)
        {
            case GunStateEnum.IDLE:
                return;
            case GunStateEnum.TARGETING:
                TargettingProcess(delta);
                break;
            case GunStateEnum.FIRING:
                FiringProcess(delta);
                break;
        }
    }

    private void TargettingProcess(double delta)
    {
        targettingTimer += delta;
        if (targettingTimer < TargettingCooldown)
            return;

        var sortedPotentialTargets = HomingTarget.Instances.OrderBy(c =>
            c.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition)
        );
        foreach (var potentialTarget in sortedPotentialTargets)
        {
            if (potentialTarget.GlobalPosition.DistanceTo(Player.GlobalPosition) > Range)
                continue;
            if (CurrentTargets.Contains(potentialTarget))
                continue;
            if (!potentialTarget.Visible)
                continue;
            if (!potentialTarget.TargetableByGuns)
                continue;
            if (
                !PlayerCamera
                    .Instances[Player.PlayerID]
                    .IsPositionInFrustum(potentialTarget.GlobalPosition)
            )
                continue;

            var spaceState = Player.GetWorld3D().DirectSpaceState;
            var rayQuery = PhysicsRayQueryParameters3D.Create(
                Player.GlobalPosition + Player.Basis.Y * 0.5f,
                potentialTarget.GlobalPosition
            );
            rayQuery.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
            var rayResult = spaceState.IntersectRay(rayQuery);

            if (rayResult.Count > 0)
            {
                if (rayResult["collider"].AsGodotObject() is Node3D collider)
                {
                    if (
                        !(
                            collider.IsAncestorOf(potentialTarget)
                            || collider.GetParent().IsAncestorOf(potentialTarget)
                            || rayResult["position"]
                                .AsVector3()
                                .DistanceTo(potentialTarget.GlobalPosition) < 0.5f
                        )
                    )
                    {
                        continue;
                    }
                }
            }

            CurrentTargets.Add(potentialTarget);
            targettingTimer = 0;
            AudioPlayer.Stream = TargettingBeepSound;
            AudioPlayer.Play(0);

            if (Style == GunStyle.Adventure)
            {
                var comboTarget = ComboTargetPrefab.Instantiate<ComboTarget>();
                GetTree().Root.AddChild(comboTarget);
                comboTarget.SetComboCount(CurrentTargets.Count);
                comboTarget.GlobalPosition = potentialTarget.GlobalPosition;
                comboTarget.CopyPositionFrom = potentialTarget;
                comboTarget.Camera = PlayerCamera.Instances[Player.PlayerID];
                CurrentComboTargets.Add(comboTarget);
            }
            break;
        }

        if (Style == GunStyle.Shadow)
        {
            fireTimer = FireCooldown;
            if (CurrentTargets.Count() == 0)
            {
                targettingTimer = 0;
            }
            FiringProcess(delta);
        }
    }

    private void FiringProcess(double delta)
    {
        if (CurrentTargets.Count == 0)
        {
            if (Style != GunStyle.Shadow)
            {
                GunState = GunStateEnum.IDLE;
                return;
            }
        }
        fireTimer += delta;
        if (fireTimer < FireCooldown)
            return;

        if (ActionRoll != null && ActionRoll.isRolling)
        {
            ActionRoll.StopRoll();
        }

        Player.PlayerSkinController.TravelAnimation("Gun Fire");

        HomingTarget nextTarget = null;
        Vector3 nextTargetGlobalPosition =
            Player.GlobalPosition
            + Player.GlobalBasis.GetRotationQuaternion() * MuzzleOffset
            + -Player.GlobalBasis.Z;
        if (CurrentTargets.Count > 0)
        {
            nextTarget = CurrentTargets[0];
            nextTargetGlobalPosition = nextTarget.GlobalPosition;
        }
        if (
            Style == GunStyle.Adventure
            && (nextTarget == null || !IsInstanceValid(nextTarget) || !nextTarget.IsInsideTree())
        )
        {
            // Happens if enemy dies before getting shot.
        }
        else
        {
            AudioPlayer.Stream = GunFireSound;
            AudioPlayer.Play(0);

            var bullet = BulletPrefab.Instantiate<HomingMissile>();
            GetTree().Root.AddChild(bullet);
            bullet.GlobalPosition =
                Player.GlobalPosition + Player.GlobalBasis.GetRotationQuaternion() * MuzzleOffset;
            bullet.LookAt(nextTargetGlobalPosition);
            bullet.OwnerPlayerActionGun = this;
            if (nextTarget != null)
            {
                bullet.Target = nextTarget;
            }

            var lookTarget =
                nextTargetGlobalPosition.ProjectOnPlane(Player.GlobalBasis.Y)
                + Player.GlobalPosition.Project(Player.GlobalBasis.Y);
            Player.LookAt(lookTarget, Player.GlobalBasis.Y);
            Player.TimedLockRotation = Mathf.Max(Player.TimedLockRotation, 0.1f);
            Player.TimedLerpedRotation = Mathf.Max(Player.TimedLerpedRotation, 0.2f);

            if (Style == GunStyle.Adventure)
            {
                CurrentComboTargets[0].QueueFree();
            }
        }

        if (CurrentTargets.Count > 0)
        {
            CurrentTargets.RemoveAt(0);
        }
        if (Style == GunStyle.Adventure)
        {
            CurrentComboTargets.RemoveAt(0);
        }

        //if (Style == GunStyle.Adventure)
        {
            fireTimer = 0;
        }
    }

    public void CancelAimingAndFiring()
    {
        if (GunState == GunStateEnum.IDLE)
            return;
        CurrentTargets.Clear();
        foreach (var comboTarget in CurrentComboTargets)
        {
            comboTarget.QueueFree();
        }
        CurrentComboTargets.Clear();
        GunState = GunStateEnum.IDLE;
    }

    public void OnSuccessfulHit(int combo)
    {
        // TODO
    }
}
