using System.ComponentModel;
using Godot;

[GlobalClass]
public partial class SplineCameraParams : CameraParams
{
    [Export]
    public NodePath CameraPath3D;

    [Export]
    public Mode SplineMode;

    public enum Mode
    {
        STRICT_3D,
        SPLINE_DIR_3D,
        SPLINE_2D,
    }

    public override void SetCameraParams(CameraTrigger trig, PlayerCamera cam)
    {
        switch (SplineMode)
        {
            case Mode.STRICT_3D:
                cam.Mode = PlayerCamera.CameraMode.Spline3DStrict;
                break;
            case Mode.SPLINE_DIR_3D:
                cam.Mode = PlayerCamera.CameraMode.Spline3D;
                break;
            case Mode.SPLINE_2D:
                cam.Mode = PlayerCamera.CameraMode.Spline2D;
                break;
            default:
                throw new InvalidEnumArgumentException(
                    "Unknown spline mode " + SplineMode.ToString()
                );
        }
        cam.Spline = trig.GetNode<Path3D>(this.CameraPath3D);
        cam.Point = null;
        cam.ConstantDirectionModifiedBySpeed = false;
    }
}
