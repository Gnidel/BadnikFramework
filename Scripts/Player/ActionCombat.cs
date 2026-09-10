using System;
using Godot;

public partial class ActionCombat : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionHoming ActionHoming;

    [Export]
    public ActionBoost ActionBoost;

    [Export]
    public float PostHomingAttackBounceStrength;

    [Export]
    public string[] PostHomingAttackAnimations = new string[0];
    private int lastPostHomingAnimation;

    public void OnSuccessfulHit(Hitbox hitbox, Hurtbox hurtbox)
    {
        bool isBoosting = ActionBoost != null && ActionBoost.IsBoosting;

        switch (hurtbox.BounceStyle)
        {
            case Hurtbox.BounceStyleEnum.BounceBack:
                Player.TimedLockRotation = 0.2f;
                Player.LinearVelocity =
                    -Player.LinearVelocity.Normalized() * PostHomingAttackBounceStrength;
                break;
            case Hurtbox.BounceStyleEnum.GoForward:
                if (!Player.Grounded)
                {
                    //if (isBoosting) { }
                    /*else*/if (
                        ActionHoming.IsHoming
                        && ActionHoming.SonicGTHomingAttack
                        && Player.PlayerInput.IsActionPressed(ActionHoming.HOMINGINPUTNAME)
                    )
                    {
                        Player.VelocityY =
                            -Player.Gravity.Normalized() * PostHomingAttackBounceStrength;
                        Player.VelocityXZ =
                            Player.VelocityXZ.Normalized() * ActionHoming.sonicGTHomingAttackSpeed;
                    }
                    else
                    {
                        Player.LinearVelocity =
                            -Player.Gravity.Normalized() * PostHomingAttackBounceStrength;
                    }
                }
                break;
            case Hurtbox.BounceStyleEnum.ConstantDir:
                Player.LinearVelocity =
                    hurtbox.ConstantDir.Normalized() * PostHomingAttackBounceStrength;
                break;
        }

        if (!isBoosting && PostHomingAttackAnimations.Length > 0 && !Player.Grounded)
        {
            var currentPostHomingAttackAnimation =
                (lastPostHomingAnimation + 1) % PostHomingAttackAnimations.Length;
            Player.PlayerSkinController.TravelAnimation(
                PostHomingAttackAnimations[currentPostHomingAttackAnimation],
                true
            );
            lastPostHomingAnimation = currentPostHomingAttackAnimation;
        }
        ActionHoming.IsHoming = false;

        if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
        {
            var camera = PlayerCamera.Instances[Player.PlayerID];
            camera.SetShake();
        }
    }
}
