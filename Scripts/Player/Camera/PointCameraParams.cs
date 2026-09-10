using System.ComponentModel;
using Godot;

[GlobalClass]
public partial class PointCameraParams : CameraParams
{
    [Export]
    public NodePath CameraPoint;

    [Export]
    public Mode PointMode;

    public enum Mode
    {
        STRICT,
        LOOK_FROM,
        LOOK_AT,
    }

    public override void SetCameraParams(CameraTrigger trig, PlayerCamera cam)
    {
        switch (PointMode)
        {
            case Mode.STRICT:
                cam.Mode = PlayerCamera.CameraMode.Point;
                break;
            case Mode.LOOK_FROM:
                cam.Mode = PlayerCamera.CameraMode.LookFromPoint;
                break;
            case Mode.LOOK_AT:
                cam.Mode = PlayerCamera.CameraMode.LookAtPoint;
                break;
            default:
                throw new InvalidEnumArgumentException(
                    "Unknown point mode " + PointMode.ToString()
                );
        }
        cam.Spline = null;
        cam.Point = trig.GetNode<Node3D>(CameraPoint);
    }
}
