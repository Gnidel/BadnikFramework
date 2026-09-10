using System;
using Godot;
using static ExtrasMenu;

public partial class ExtraSettingsApplicator : Node
{
    public override void _Ready()
    {
        ExtrasMenu.Options = ExtrasFile.Load();
    }

    public void ApplySettings()
    {
        foreach (var player in PlayerController.Instances)
        {
            var actionBoost = player.GetNodeOrNull<ActionBoost>(
                new NodePath("./PlayerControl/Actions/ActionBoost")
            );
            if (actionBoost != null && actionBoost.ProcessMode != ProcessModeEnum.Disabled)
            {
                actionBoost.ProcessMode = ExtrasMenu.Options.BoostEnabled
                    ? ProcessModeEnum.Inherit
                    : ProcessModeEnum.Disabled;
            }

            var actionSuper = player.GetNodeOrNull<ActionSuperTransformation>(
                new NodePath("./PlayerControl/Actions/ActionSuperTransformation")
            );
            if (actionSuper != null && actionSuper.ProcessMode != ProcessModeEnum.Disabled)
            {
                actionSuper.Flying = ExtrasMenu.Options.SuperFly;
            }
        }

        PlayersManager.Instance.CharacterSwappingEnabled = ExtrasMenu.Options.HeroesSwap;
    }
}
