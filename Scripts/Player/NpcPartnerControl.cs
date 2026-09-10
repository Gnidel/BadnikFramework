using System;
using System.Linq;
using Godot;

public partial class NpcPartnerControl : Node
{
    [Export]
    public bool IsNpc;

    [Export]
    public PlayerController Player;

    [Export]
    public PlayerInput PlayerInput;

    [Export]
    public Node3D Target;

    [Export]
    public float ExpectedDistanceFromTarget = 5;

    //[Export] public Camera3D Camera;
    [Export]
    public float VelocityAfterTeleport = 30f;

    [Export]
    public float RespawnAfterOffscreenFor = 3f;

    private float respawnAfterOffscreenTimer = 0;

    public override void _Process(double delta)
    {
        if (!IsNpc)
            return;
        if (Target == null)
            return;
        var TargetPlayer = Target.GetNodeOrNull<PlayerController>(".");

        // Following target
        var targetVelocity = TargetPlayer != null ? TargetPlayer.LinearVelocity : Vector3.Zero;
        var targetOffset = (
            (Target.GlobalPosition + targetVelocity.ProjectOnPlane(Target.Basis.Y))
            - Player.GlobalPosition
        );
        if (targetOffset.Length() > ExpectedDistanceFromTarget)
        {
            var leftInput = targetOffset.ProjectOnPlane(Player.GroundNormal).Normalized();
            if (
                TargetPlayer != null
                && targetOffset.Normalized().Dot(-Target.Basis.Z) > 0
                && Player.LinearVelocity.Length() > targetVelocity.Length()
            )
            {
                leftInput *= 0.6f;
            }
            PlayerInput.NpcSetLeftInput(leftInput);
        }
        else
        {
            PlayerInput.NpcSetLeftInput(Vector3.Zero);
        }

        // Offscreen teleportation
        bool visibleInAnyCamera = false;
        foreach (var camera in PlayerCamera.Instances)
        {
            if (
                camera.Value.IsPositionInFrustum(Player.GlobalPosition)
                && !Player.PlayerDamage.IsDead
            )
            {
                respawnAfterOffscreenTimer = 0;
                visibleInAnyCamera = true;
                break;
            }
        }
        if (!visibleInAnyCamera)
        {
            respawnAfterOffscreenTimer += (float)delta;
        }
        if (respawnAfterOffscreenTimer > RespawnAfterOffscreenFor)
        {
            Respawn();
        }

        // Jumping

        if (
            targetOffset.Project(Player.GlobalBasis.Y).Length() > 1
            && targetOffset.Dot(Player.GlobalBasis.Y) > 0
        )
        {
            if (Player.Grounded || Player.VelocityYDirected < 1)
            {
                PlayerInput.NpcPressButton("actionjump");
            }
            else
            {
                PlayerInput.NpcReleaseButton("actionjump");
            }
        }

        // Rolling
        var actionRoll = Player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        if (actionRoll != null && actionRoll.CanManuallyRoll && Player.Grounded)
        {
            if (
                Player.Grounded
                && actionRoll.isRolling
                && (-Player.GlobalBasis.Z).Dot(-Player.Gravity.Normalized()) > 0
            )
            {
                PlayerInput.NpcReleaseButton("actionroll");
            }
            else if (
                Player.Grounded
                && !actionRoll.isRolling
                && (-Player.GlobalBasis.Z).Dot(-Player.Gravity.Normalized()) < -0.3f
            )
            {
                PlayerInput.NpcPressButton("actionroll");
            }
        }

        // Removing drift
        var actionDrift = Player.GetNodeOrNull<ActionDrift>("./PlayerControl/Actions/ActionDrift");
        if (actionDrift != null)
        {
            if (actionDrift.IsDrifting && Mathf.Abs(Player.Leaning) > 0.5f)
            {
                PlayerInput.NpcReleaseButton("actionroll");
            }
        }
    }

    void Respawn()
    {
        var TargetPlayer = Target.GetNodeOrNull<PlayerController>(".");
        respawnAfterOffscreenTimer = 0;
        var cam = PlayerCamera.Instances.First().Value;
        if (TargetPlayer != null && TargetPlayer.PlayerInput.TwodimensionalMode)
        {
            Player.GlobalPosition = Target.GlobalPosition;
            Player.LinearVelocity = Vector3.Zero;
        }
        else
        {
            var targetPlayer = Target.GetNodeOrNull<PlayerController>(".");
            if (targetPlayer != null)
            {
                Player.GlobalPosition = cam.GlobalPosition + cam.Basis.Z;
                // Transfer spline locks
                SplineMovement currentSplineMovement = targetPlayer.GetNodeOrNull<SplineMovement>(
                    "./PlayerControl/SplineMovement"
                );
                SplineMovement nextSplineMovement = Player.GetNodeOrNull<SplineMovement>(
                    "./PlayerControl/SplineMovement"
                );
                if (currentSplineMovement != null && nextSplineMovement != null)
                {
                    nextSplineMovement.CopyCustomFieldsFrom(currentSplineMovement);
                }
            }
            else
            {
                Player.GlobalPosition = Target.GlobalPosition;
            }
            var targetRb = Target.GetNodeOrNull<RigidBody3D>(".");
            if (targetRb != null)
            {
                Player.LinearVelocity =
                    targetRb.LinearVelocity + -cam.Basis.Z * VelocityAfterTeleport;
            }
        }
        Player.PlayerDamage.IsDead = false;
        Player.PlayerSkinController.TravelAnimation("Idle");
    }
}
