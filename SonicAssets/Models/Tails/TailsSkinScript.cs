using System;
using Godot;

public partial class TailsSkinScript : Node
{
    [Export]
    public MeshInstance3D GunModel;

    [Export]
    public PlayerSkinController PlayerSkinController;

    [Export]
    public double FadeSpeed = 5;
    private ActionGun ActionGun;

    public override void _Ready()
    {
        ActionGun = PlayerSkinController.PlayerController.GetNodeOrNull<ActionGun>(
            "./PlayerControl/Actions/ActionGun"
        );
    }

    public override void _Process(double delta)
    {
        float targetGunFadeout = 0;
        if (ActionGun.GunState != ActionGun.GunStateEnum.IDLE)
        {
            targetGunFadeout = 1;
        }
        var currentFade = GunModel.GetActiveMaterial(0).Get("shader_parameter/Fading").AsDouble();
        if (currentFade > targetGunFadeout)
        {
            currentFade -= delta * FadeSpeed;
        }
        else if (currentFade < targetGunFadeout)
        {
            currentFade += delta * FadeSpeed;
        }

        GunModel.GetActiveMaterial(0).Set("shader_parameter/Fading", currentFade);
    }
}
