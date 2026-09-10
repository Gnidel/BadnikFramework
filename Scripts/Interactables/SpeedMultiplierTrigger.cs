using System;
using Godot;

public partial class SpeedMultiplierTrigger : Area3D
{
    [Export]
    public float SpeedMultiplier = 1;

    public override void _Ready()
    {
        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.BodyEntered += this.OnBodyEnter;
        }

        //if (GetSignalConnectionList("body_exited").Count == 0)
        //{
        //    this.BodyExited += this.OnBodyExited;
        //}
    }

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            var actionBoost = player.GetNodeOrNull<ActionBoost>(
                "./PlayerControl/Actions/ActionBoost"
            );
            if (actionBoost != null)
            {
                actionBoost.MaxBoostSpeed /= player.SpeedMultiplier;
                actionBoost.MaxBoostSpeed *= this.SpeedMultiplier;
            }

            player.VelocityXZ /= player.SpeedMultiplier;
            player.VelocityXZ *= this.SpeedMultiplier;

            player.SpeedMultiplier = this.SpeedMultiplier;
        }
    }
}
