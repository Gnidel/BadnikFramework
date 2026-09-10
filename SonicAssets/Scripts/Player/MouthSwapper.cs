using System;
using System.Linq;
using Godot;

public partial class MouthSwapper : Node
{
    [Export]
    public Skeleton3D Skeleton;

    [Export]
    public int LeftMouthBoneIdx;

    [Export]
    public int RightMouthBoneIdx;

    [Export]
    public float Threshold;

    public override void _Ready()
    {
        Skeleton.SetBonePoseScale(LeftMouthBoneIdx, Vector3.One);
        Skeleton.SetBonePoseScale(RightMouthBoneIdx, Vector3.Zero);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (PlayerCamera.Instances.Count == 0)
            return;
        PlayerCamera nearestCamera = PlayerCamera.Instances.Values.First();
        foreach (var cam in PlayerCamera.Instances.Values)
        {
            if (
                cam.GlobalPosition.DistanceTo(Skeleton.GlobalPosition)
                < nearestCamera.GlobalPosition.DistanceTo(Skeleton.GlobalPosition)
            )
            {
                nearestCamera = cam;
            }
        }

        var dot = nearestCamera.GlobalBasis.Z.Dot(Skeleton.GlobalBasis.X);
        bool rightMouth = false;
        bool leftMouth = false;
        if (dot > Threshold)
        {
            leftMouth = true;
        }
        else if (dot < -Threshold)
        {
            rightMouth = true;
        }
        if (leftMouth)
        {
            Skeleton.SetBonePoseScale(LeftMouthBoneIdx, Vector3.One);
            Skeleton.SetBonePoseScale(RightMouthBoneIdx, Vector3.Zero);
        }
        else if (rightMouth)
        {
            Skeleton.SetBonePoseScale(LeftMouthBoneIdx, Vector3.Zero);
            Skeleton.SetBonePoseScale(RightMouthBoneIdx, Vector3.One);
        }
    }
}
