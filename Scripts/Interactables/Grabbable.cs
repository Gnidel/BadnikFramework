using System;
using System.Collections.Generic;
using Godot;

public partial class Grabbable : Node3D
{
    [Export]
    public string Animation = "Pulley";

    [Export]
    public string EndAnimation = "Air";

    [Export]
    public PathFollow3D PathFollow3D;

    [Export]
    public float Speed = 1;

    [Export]
    public bool CanJumpOff = true;

    [Export]
    public Node3D PlayerLockPosition;

    [Export]
    public HomingTarget HomingTarget;

    [Export]
    public Vector3 RelativeEjectVelocity = Vector3.Up * 10;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public RemoteTransform3D RemoteTransform3D;

    [Export]
    public Node3D CameraCenterPosition;

    [Export]
    public Node3D CameraLookFrom;

    [Export]
    public float Cooldown = 0.5f;

    private bool active = false;
    private float cooldownTimer = 0;
    public ActionAutoGimmick currentPlayer;

    public override void _PhysicsProcess(double delta)
    {
        if (!active)
            cooldownTimer += (float)delta;

        bool shouldEnd = (
            currentPlayer != null
            && CanJumpOff
            && currentPlayer.Player.PlayerInput.IsActionJustPressed(
                ActionJump.JUMP_INPUT_NAME,
                true
            )
        );
        if (PathFollow3D != null)
        {
            if (active)
            {
                PathFollow3D.Progress = PathFollow3D.Progress + Speed * (float)delta;
            }
            else
            {
                PathFollow3D.Progress = PathFollow3D.Progress - Speed * (float)delta;
            }
            if (PathFollow3D.ProgressRatio >= 1)
            {
                shouldEnd = true;
            }
        }

        if (currentPlayer != null && shouldEnd)
        {
            EjectPlayer();
        }

        if (HomingTarget != null)
        {
            HomingTarget.Visible = !active;
            HomingTarget.ProcessMode = active ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        }
    }

    public void EjectPlayer()
    {
        RemoteTransform3D.RemotePath = null;
        currentPlayer.EndAutoGimmick(EndAnimation);
        currentPlayer.Player.LinearVelocity =
            this.GlobalBasis.GetRotationQuaternion() * RelativeEjectVelocity;
        currentPlayer.Player.GlobalRotation = PlayerLockPosition.GlobalRotation;
        currentPlayer.Player.TimedLerpedRotation = 0.5f;
        active = false;
        cooldownTimer = 0;
        if (AudioPlayer != null)
        {
            AudioPlayer.Stop();
        }

        var cam = PlayerCamera.Instances.GetValueOrDefault(currentPlayer.Player.PlayerID, null);
        if (cam != null && CameraCenterPosition != null)
        {
            cam.CameraTargets.Remove(CameraCenterPosition);
            cam.CameraTargets.Add(currentPlayer.Player);
        }

        if (
            cam != null
            && CameraLookFrom != null
            && cam.Mode == PlayerCamera.CameraMode.Point
            && cam.Point == CameraLookFrom
        )
        {
            cam.Mode = PlayerCamera.CameraMode.Free;
            cam.Point = null;
        }

        EmitSignal(SignalName.OnPlayerDrop, currentPlayer.Player);

        currentPlayer = null;
    }

    public void OnCollissionEnter(Node3D other)
    {
        if (currentPlayer != null || active || cooldownTimer < Cooldown)
            return;

        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            var actionAutoGimmick = player.GetNodeOrNull<ActionAutoGimmick>(
                "./PlayerControl/Actions/ActionAutoGimmick"
            );
            if (actionAutoGimmick != null)
            {
                currentPlayer = actionAutoGimmick;
                actionAutoGimmick.StartAutoGimmick(Animation, PlayerLockPosition);
                active = true;
                if (AudioPlayer != null)
                {
                    AudioPlayer.Play();
                }
                RemoteTransform3D.RemotePath = player.GetPath();

                var cam = PlayerCamera.Instances.GetValueOrDefault(
                    currentPlayer.Player.PlayerID,
                    null
                );
                if (cam != null && CameraCenterPosition != null)
                {
                    cam.CameraTargets.Add(CameraCenterPosition);
                    cam.CameraTargets.Remove(currentPlayer.Player);
                }

                if (
                    cam != null
                    && CameraLookFrom != null
                    && cam.Mode == PlayerCamera.CameraMode.Free
                )
                {
                    cam.Mode = PlayerCamera.CameraMode.Point;
                    cam.Point = CameraLookFrom;
                    cam.StartLerp(0.1f);
                }
            }
            EmitSignal(SignalName.OnPlayerGrab, player);
        }
    }

    [Signal]
    public delegate void OnPlayerGrabEventHandler(PlayerController player);

    [Signal]
    public delegate void OnPlayerDropEventHandler(PlayerController player);
}
