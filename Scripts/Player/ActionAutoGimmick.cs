using System;
using Godot;

/* 	This is basically anything where player loses control and plays an animation,
    For example, it can be used for pulleys, Speed Highway rockets or vehicles.*/
public partial class ActionAutoGimmick : Node
{
    [Export]
    public PlayerController Player;
    private bool actionActive = false;
    private Node3D copyPos = null;
    private bool keepPhysics = false;

    public override void _PhysicsProcess(double delta)
    {
        if (!actionActive)
            return;
        if (copyPos != null)
        {
            Player.GlobalTransform = copyPos.GlobalTransform;
        }
    }

    public void StartAutoGimmick(
        string AnimationName,
        Node3D copyPos = null,
        bool keepPhysics = false
    )
    {
        if (!(Player.PlayerSkinController.SkipRankWaitAnimation && AnimationName == "Rank Wait"))
        {
            Player.PlayerSkinController.TravelAnimation(AnimationName);
        }
        Player.LinearVelocity = Vector3.Zero;
        Player.PlayerInput.LockedInput = true;
        this.keepPhysics = keepPhysics;
        if (!keepPhysics)
        {
            Player.Enabled = false;
            Player.SetPhysicsProcess(false);
        }
        this.copyPos = copyPos;

        var homing = Player.GetNodeOrNull<ActionHoming>("./PlayerControl/Actions/ActionHoming");
        if (homing != null)
        {
            homing.IsHoming = false;
        }
        var rolling = Player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        if (rolling != null)
        {
            rolling.StopRoll();
        }
        actionActive = true;
    }

    public void EndAutoGimmick(string AnimationName)
    {
        Player.PlayerSkinController.TravelAnimation(AnimationName);
        Player.Enabled = true;
        copyPos = null;
        Player.PlayerInput.LockedInput = false;
        Player.SetPhysicsProcess(true);
        actionActive = false;
    }
}
