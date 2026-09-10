using Godot;

[GlobalClass]
public abstract partial class CameraParams : Resource
{
    public abstract void SetCameraParams(CameraTrigger trig, PlayerCamera cam);
}
