using Godot;

[GlobalClass]
public partial class FreeCameraParams : CameraParams
{
    public override void SetCameraParams(CameraTrigger trig, PlayerCamera cam)
    {
        cam.Mode = PlayerCamera.CameraMode.Free;
        cam.Spline = null;
        cam.Point = null;
    }
}
