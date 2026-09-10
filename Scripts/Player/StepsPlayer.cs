using System;
using Godot;

public partial class StepsPlayer : Node
{
    private PlayerSkinController PlayerSkinController;

    [Export]
    public AudioStreamPlayer3D AudioStreamPlayer;

    [Export]
    public AudioStream LeftStepDefaultSound;

    [Export]
    public AudioStream RightStepDefaultSound;

    [Export]
    public AudioStream LeftStepWaterSound;

    [Export]
    public AudioStream RightStepWaterSound;

    [Export]
    public AudioStream LeftStepDirtSound;

    [Export]
    public AudioStream RightStepDirtSound;

    [Export]
    public AudioStream LeftStepMetalSound;

    [Export]
    public AudioStream RightStepMetalSound;

    [Export]
    public AudioStream LeftStepRockSound;

    [Export]
    public AudioStream RightStepRockSound;

    [Export]
    public AudioStream LeftStepWoodSound;

    [Export]
    public AudioStream RightStepWoodSound;

    private WaterInteraction WaterInteraction;

    public override void _Ready()
    {
        if (PlayerSkinController == null)
        {
            PlayerSkinController = this.GetNodeOrNull<PlayerSkinController>("..");
        }
        if (PlayerSkinController != null && WaterInteraction == null)
        {
            WaterInteraction =
                PlayerSkinController.PlayerController.GetNodeOrNull<WaterInteraction>(
                    "./PlayerControl/WaterInteraction"
                );
        }
    }

    public void PlayLeftStep()
    {
        AudioStreamPlayer.Stream = GetLeftStepSound();
        AudioStreamPlayer.Play(0);
    }

    public void PlayRightStep()
    {
        AudioStreamPlayer.Stream = GetRightStepSound();
        AudioStreamPlayer.Play(0);
    }

    private AudioStream GetLeftStepSound()
    {
        if (WaterInteraction != null && WaterInteraction.WaterFeetCounter > 0)
        {
            return LeftStepWaterSound;
        }
        else if (
            LeftStepDirtSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 20)) > 0
        )
        {
            return LeftStepDirtSound;
        }
        else if (
            LeftStepMetalSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 21)) > 0
        )
        {
            return LeftStepMetalSound;
        }
        else if (
            LeftStepRockSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 22)) > 0
        )
        {
            return LeftStepRockSound;
        }
        else if (
            LeftStepWoodSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 23)) > 0
        )
        {
            return LeftStepWoodSound;
        }
        return LeftStepDefaultSound;
    }

    private AudioStream GetRightStepSound()
    {
        if (WaterInteraction != null && WaterInteraction.WaterFeetCounter > 0)
        {
            return RightStepWaterSound;
        }
        else if (
            RightStepDirtSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 20)) > 0
        )
        {
            return RightStepDirtSound;
        }
        else if (
            RightStepMetalSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 21)) > 0
        )
        {
            return RightStepMetalSound;
        }
        else if (
            RightStepRockSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 22)) > 0
        )
        {
            return RightStepRockSound;
        }
        else if (
            RightStepWoodSound != null
            && (PlayerSkinController.PlayerController.ColliderLayer & (1 << 23)) > 0
        )
        {
            return RightStepWoodSound;
        }
        return RightStepDefaultSound;
    }
}
