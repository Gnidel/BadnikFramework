using Godot;

[GlobalClass]
public partial class ConstantCameraParams : CameraParams
{
    [Export]
    public Quaternion ConstantCameraRotation = Quaternion.Identity;

    [Export]
    public bool ConstantDirectionModifiedBySpeed = false;

    [Export]
    public bool Snap = false;

    public override void SetCameraParams(CameraTrigger trig, PlayerCamera cam)
    {
        cam.Mode = PlayerCamera.CameraMode.Constant;
        cam.Spline = null;
        cam.Point = null;

        cam.ConstantCameraDirection =
            trig.GlobalBasis.GetRotationQuaternion().Normalized()
            * this.ConstantCameraRotation.Normalized();
        cam.ConstantDirectionModifiedBySpeed = this.ConstantDirectionModifiedBySpeed;

        if (this.Snap)
        {
            cam.GlobalBasis = new Basis(cam.ConstantCameraDirection.Normalized());
        }
    }
}
