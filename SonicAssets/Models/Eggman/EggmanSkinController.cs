using System;
using System.Security.Cryptography.X509Certificates;
using Godot;

public partial class EggmanSkinController : Node3D
{
    [Export]
    public AnimationTree EggmanAnimationTree;

    [Export]
    public AnimationPlayer EggmanAnimationPlayer;

    [Export]
    public Skeleton3D EggmanSkeleton;

    [Export]
    public Skeleton3D EggMobileSkeleton;

    [Export]
    public string HandLBoneName = "Hand_L";

    [Export]
    public string HandRBoneName = "Hand_R";

    [Export]
    public string HandlePiller1LBoneName = "HandlePiller2_L";

    [Export]
    public string HandlePiller1RBoneName = "HandlePiller2_R";

    public override void _Process(double delta)
    {
        //// Left handle
        //var boneIdx = EggmanSkeleton.FindBone(HandLBoneName);
        //var bonePos = EggmanSkeleton.GlobalTransform * EggmanSkeleton.GetBoneGlobalPose(boneIdx);

        //var handleBoneIdx = EggMobileSkeleton.FindBone(HandlePiller1LBoneName);
        //var newHandlePos = EggMobileSkeleton.GlobalTransform * EggMobileSkeleton.GetBoneGlobalPose(handleBoneIdx);
        //newHandlePos.Origin = bonePos.Origin + EggMobileSkeleton.GlobalTransform.Basis.GetRotationQuaternion().Normalized() * EggMobileSkeleton.GetBoneGlobalRest(handleBoneIdx).Origin;
        //EggMobileSkeleton.SetBoneGlobalPoseOverride(handleBoneIdx, EggMobileSkeleton.GlobalTransform.AffineInverse() * newHandlePos, 1, true);

        ////// Right handle
        ////boneIdx = EggmanSkeleton.FindBone(HandRBoneName);
        ////bonePos = EggmanSkeleton.GlobalTransform * EggmanSkeleton.GetBoneGlobalPose(boneIdx);

        ////handleBoneIdx = EggMobileSkeleton.FindBone(HandlePiller1RBoneName);
        ////EggMobileSkeleton.SetBoneGlobalPoseOverride(handleBoneIdx, EggmanSkeleton.GlobalTransform.AffineInverse() * bonePos, 1, true);

        //AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)EggmanAnimationTree.Get("parameters/playback");
        //stateMachine.Travel("eg_atk_start");
    }

    void OnAggro()
    {
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            EggmanAnimationTree.Get("parameters/playback");
        stateMachine.Travel("Aggro");
    }

    void OnAttack()
    {
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            EggmanAnimationTree.Get("parameters/playback");
        stateMachine.Travel("Attack", true);
    }

    void OnDamage(EnemyControl enemyControl)
    {
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            EggmanAnimationTree.Get("parameters/playback");
        stateMachine.Travel("Damage", true);
    }

    void OnDeath(EnemyControl enemyControl)
    {
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            EggmanAnimationTree.Get("parameters/playback");
        stateMachine.Travel("Dead", true);
    }
}
