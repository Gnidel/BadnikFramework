using System;
using Godot;

public partial class GlobalItem : Node3D
{
	[Export]
	public CollectableItem CollectableItem;

	[Export]
	public uint RandomGlobalFlagID = 0;

	public override void _Ready()
	{
		if (CollectableItem == null)
			CollectableItem = this.GetParent<CollectableItem>();
		if (CollectableItem == null)
		{
			GD.PrintErr("CollectableItem in GlobalItem is null!");
			return;
		}
		if (RandomGlobalFlagID == 0)
		{
			RandomGlobalFlagID = GenerateId();
		}
		SaveData save = SaveData.Load(); // TODO: THIS IS SO STUPIDLY UNOPTIMIZED I JUST CAN'T, potentially hundrends of JSON deserializations
        if (
            save.GlobalFlags.ContainsKey(RandomGlobalFlagID.ToString())
            && save.GlobalFlags[RandomGlobalFlagID.ToString()] != 0
        )
        {
            CollectableItem.QueueFree();
        }
        else
        {
            CollectableItem.OnItemCollected += this.OnCollect;
        }
    }

    uint GenerateId()
    {
        return (this.GetTree().CurrentScene.SceneFilePath + "\\" + this.GetPath()).Hash();
    }

    public void OnCollect(PlayerController player)
    {
        var save = SaveData.Load();
        save.GlobalFlags[RandomGlobalFlagID.ToString()] = 1;
        save.GlobalItems[CollectableItem.ItemName] = player.PlayerInventory.GetItemCount(
            CollectableItem.ItemName
        );
        save.Save();
    }
}
