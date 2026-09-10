using System;
using Godot;
using Godot.Collections;

public partial class OneTimeCollectableAltModel : Node
{
    [Export]
    public Node3D CollectedModel;

    [Export]
    public Node3D UncollectedModel;

    [Export]
    public string ItemName;

    public override void _Ready()
    {
        UncollectedModel.Visible = true;
        UncollectedModel.ProcessMode = ProcessModeEnum.Inherit;
        CollectedModel.Visible = false;
        CollectedModel.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var player in PlayerController.Instances)
        {
            if (player.PlayerInventory.GetItemCount(ItemName) > 0)
            {
                UncollectedModel.Visible = false;
                UncollectedModel.ProcessMode = ProcessModeEnum.Disabled;
                CollectedModel.Visible = true;
                CollectedModel.ProcessMode = ProcessModeEnum.Inherit;
                break;
            }
        }
    }
}
