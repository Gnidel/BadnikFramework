using System;
using Godot;

public partial class CameraTrigger : Area3D
{
    [Export]
    public CameraParams Params;
    PlayerCamera.CameraMode Mode = PlayerCamera.CameraMode.Free;
    Quaternion ConstantCameraRotation;
    Path3D CameraCurve = null;
    public Node3D CameraPoint;

    [Export]
    bool DisableRaycast = false;

    [Export]
    float CameraDistance = -1; // Negative values mean don't change

    [Export]
    public bool CardinalDirectionBias = false;

    [Export]
    public bool FreeOnExit = false; // Keep in mind it doesn't work with ConcavePolygonShape.

    [Export]
    public float EnterLerpTime = 0f;

    [Export]
    public float ExitLerpTime = 0f; // Only applicable for FreeOnExit

    [Export]
    public CameraTrigger OnExitTrigger;

    public override void _Ready()
    {
        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.BodyEntered += this.OnBodyEnter;
        }

        if (GetSignalConnectionList("body_exited").Count == 0)
        {
            this.BodyExited += this.OnBodyExited;
        }

        const int PLAYER_LAYER = 2;
        this.SetCollisionMaskValue(PLAYER_LAYER, true);
    }

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null && !StageData.Instance.IsFinished)
        {
            if (PlayerCamera.Instances.ContainsKey(player.PlayerID))
            {
                var camera = PlayerCamera.Instances[player.PlayerID];

                if (EnterLerpTime > 0)
                {
                    camera.StartLerp(EnterLerpTime);
                }
                SetCamera(camera);
            }
        }
    }

    public void OnBodyExited(Node3D other)
    {
        if (OnExitTrigger != null)
        {
            OnExitTrigger.OnBodyEnter(other);
            return;
        }
        if (!FreeOnExit)
            return;

        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            if (PlayerCamera.Instances.ContainsKey(player.PlayerID))
            {
                var camera = PlayerCamera.Instances[player.PlayerID];

                camera.Mode = PlayerCamera.CameraMode.Free;
                camera.Spline = null;
                camera.Point = null;

                if (ExitLerpTime > 0)
                {
                    camera.StartLerp(ExitLerpTime);
                }
            }
        }
    }

    void SetCamera(PlayerCamera cam)
    {
        cam.DisableRaycast = this.DisableRaycast;
        cam.CardinalDirectionBias = this.CardinalDirectionBias;
        if (this.CameraDistance >= 0)
        {
            cam.MaxCameraDistance = this.CameraDistance;
        }

        if (Params != null)
        {
            Params.SetCameraParams(this, cam);
            return;
        }

        cam.Mode = this.Mode;
        cam.Spline = null;
        cam.Point = null;
        if (cam.Mode == PlayerCamera.CameraMode.Constant)
        {
            cam.ConstantCameraDirection = this.ConstantCameraRotation.Normalized();
        }
        else if (
            cam.Mode == PlayerCamera.CameraMode.Spline3D
            || cam.Mode == PlayerCamera.CameraMode.Spline3DStrict
            || cam.Mode == PlayerCamera.CameraMode.Spline2D
        )
        {
            cam.Spline = this.CameraCurve;
        }
        else if (
            cam.Mode == PlayerCamera.CameraMode.Point
            || cam.Mode == PlayerCamera.CameraMode.LookFromPoint
        )
        {
            cam.Point = this.CameraPoint;
        }
    }
}
