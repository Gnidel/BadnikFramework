using System;
using Godot;

public partial class Running2DBossArenaPlayerProperties : Node3D
{
    // Player manipulation
    [Export]
    public Vector3 ConstantPush;

    [Export]
    public float BoostSpeed = 30f;

    [Export]
    public float SpeedMultiplier = 0.25f;

    [Export]
    public float JumpVelocity = 10f;

    public override void _PhysicsProcess(double delta)
    {
        if (StageData.Instance.IsFinished)
            return;
        // Player manipulation
        foreach (var player in PlayerController.Instances)
        {
            player.SpeedMultiplier = SpeedMultiplier;
            var actionJump = player.GetNodeOrNull<ActionJump>("./PlayerControl/Actions/ActionJump");
            if (actionJump != null)
            {
                actionJump.JumpVelocity = JumpVelocity;
            }

            player.MoveAndCollide(ConstantPush * (float)delta);
            var actionBoost = player.GetNodeOrNull<ActionBoost>(
                "./PlayerControl/Actions/ActionBoost"
            );
            if (actionBoost != null)
            {
                actionBoost.MinBoostSpeed = BoostSpeed;
            }
        }
    }
}
