using System;
using Godot;
using Godot.Collections;

public partial class SkydivingSectionTrigger : Area3D
{
    [Export]
    public float SlowSkydiveSpeed = 10;

    [Export]
    public float FastSkydiveSpeed = 100;

    [Export]
    public bool ChangeCamera = true; // It's recommended to set it to false in 2D sections.

    public Array<PlayerController> Players = new Array<PlayerController>();

    public override void _Ready()
    {
        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.BodyEntered += this.OnAreaEnter;
        }
        if (GetSignalConnectionList("body_exited").Count == 0)
        {
            this.BodyExited += this.OnAreaExit;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var player in Players)
        {
            if (player.Grounded)
            {
                StopPlayerSkydive(player);
                return;
            }

            bool isFastDiving = player.PlayerInput.IsActionPressed("actiondash", true);
            var currentAnim = player.PlayerSkinController.GetParameter(
                "parameters/Skydiving/DiveSpeed/blend_amount"
            );
            player.PlayerSkinController.SetParameter(
                "parameters/Skydiving/DiveSpeed/blend_amount",
                Mathf.Lerp(currentAnim, isFastDiving ? 1 : 0, delta * 10)
            );

            var actionHoming = player.GetNode<ActionHoming>("./PlayerControl/Actions/ActionHoming");
            if (actionHoming == null || !actionHoming.IsHoming)
            {
                player.PlayerSkinController.TravelAnimation("Skydiving");
            }

            bool isRolling = false;
            var actionRoll = player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
            if (actionRoll != null)
            {
                if (actionRoll.isRolling)
                    isRolling = true;
            }

            if (!isRolling)
            {
                if (isFastDiving)
                {
                    player.VelocityY = player.VelocityY.Lerp(
                        player.Gravity.Normalized() * FastSkydiveSpeed,
                        (float)delta
                    );
                }
                else if (player.VelocityYDirected < 0)
                {
                    player.VelocityY = player.VelocityY.Lerp(
                        player.Gravity.Normalized() * SlowSkydiveSpeed,
                        (float)delta
                    );
                }
            }

            var actionDash = player.GetNodeOrNull<ActionDash>("./PlayerControl/Actions/ActionDash");
            if (actionDash != null)
            {
                actionDash.airDashCount = actionDash.MaxAllowedAirDashes;
            }

            var actionBoost = player.GetNodeOrNull<ActionBoost>(
                "./PlayerControl/Actions/ActionBoost"
            );
            if (actionBoost != null)
            {
                actionBoost.IsBoosting = false;
            }

            var actionJump = player.GetNodeOrNull<ActionJump>("./PlayerControl/Actions/ActionJump");
            if (actionJump != null)
            {
                actionJump.JumpCount = actionJump.MaxJumpCount;
            }

            var actionStomp = player.GetNodeOrNull<ActionStomp>(
                "./PlayerControl/Actions/ActionStomp"
            );
            if (actionStomp != null)
            {
                actionStomp.IsStomping = false;
                actionStomp.LockedAction = true;
            }
        }
    }

    public void OnAreaEnter(Node3D other)
    {
        var Player = other.GetNodeOrNull<PlayerController>(".");
        if (Player != null && !Players.Contains(Player))
        {
            StartPlayerSkydive(Player);
        }
    }

    public void OnAreaExit(Node3D other)
    {
        var Player = other.GetNodeOrNull<PlayerController>(".");
        if (Player != null && Players.Contains(Player))
        {
            StopPlayerSkydive(Player);
        }
    }

    void StartPlayerSkydive(PlayerController Player)
    {
        var actionAutoGimmick = Player.GetNodeOrNull<ActionAutoGimmick>(
            "./PlayerControl/Actions/ActionAutoGimmick"
        );
        if (actionAutoGimmick == null)
        {
            return;
        }
        Players.Add(Player);
        //actionAutoGimmick.StartAutoGimmick("Skydiving");
        //Player.SetPhysicsProcess(true);
        Player.PlayerSkinController.TravelAnimation("Skydiving", true);

        if (
            ChangeCamera
            && PlayerCamera.Instances.ContainsKey(Player.PlayerID)
            && PlayerCamera.Instances[Player.PlayerID].Mode == PlayerCamera.CameraMode.Free
        )
        {
            PlayerCamera.Instances[Player.PlayerID].Mode = PlayerCamera.CameraMode.Constant;
            PlayerCamera.Instances[Player.PlayerID].ConstantCameraDirection = Transform3D
                .Identity.LookingAt(-Player.Gravity.Normalized(), this.GlobalBasis.X)
                .Basis.GetRotationQuaternion();
            PlayerCamera.Instances[Player.PlayerID].CameraOffset = Vector3.Zero;
            PlayerCamera.Instances[Player.PlayerID].ConstantDirectionModifiedBySpeed = false;
        }
    }

    void StopPlayerSkydive(PlayerController Player)
    {
        Players.Remove(Player);

        Player.PlayerSkinController.TravelAnimation("Idle", true);

        if (ChangeCamera && PlayerCamera.Instances.ContainsKey(Player.PlayerID))
        {
            PlayerCamera.Instances[Player.PlayerID].Mode = PlayerCamera.CameraMode.Free;
            PlayerCamera.Instances[Player.PlayerID].ConstantCameraDirection = Quaternion.Identity;
            PlayerCamera.Instances[Player.PlayerID].CameraOffset = new Vector3(0, 1.5f, 0); // TODO: Assume defaults changed
        }

        var actionStomp = Player.GetNodeOrNull<ActionStomp>("./PlayerControl/Actions/ActionStomp");
        if (actionStomp != null)
        {
            actionStomp.LockedAction = false;
        }
    }
}
