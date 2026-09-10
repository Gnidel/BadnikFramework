using System;
using Godot;

public partial class PlayerActions : Node
{
    public void ResetActions()
    {
        var rollAction = this.GetNodeOrNull<ActionRoll>("./ActionRoll");
        if (rollAction != null)
        {
            rollAction.SetRoll(false);
        }

        var homingAction = this.GetNodeOrNull<ActionHoming>("./ActionHoming");
        if (homingAction != null)
        {
            homingAction.IsHoming = false;
        }

        var actionJump = this.GetNodeOrNull<ActionJump>("./ActionJump");
        if (actionJump != null)
        {
            actionJump.JumpCount = 1;
        }

        var actionDash = this.GetNodeOrNull<ActionDash>("./ActionDash");
        if (actionDash != null)
        {
            actionDash.airDashCount = 0;
        }

        var actionBoost = this.GetNodeOrNull<ActionBoost>("./ActionBoost");
        if (actionBoost != null)
        {
            actionBoost.StopBoost();
        }

        var actionStomp = this.GetNodeOrNull<ActionStomp>("./ActionStomp");
        if (actionStomp != null)
        {
            actionStomp.CancelStomp();
        }

        var actionGun = this.GetNodeOrNull<ActionGun>("./ActionGun");
        if (actionGun != null)
        {
            actionGun.CancelAimingAndFiring();
        }
    }
}
