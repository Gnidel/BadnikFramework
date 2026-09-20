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
        SaveData save = GlobalItemSystem.Load();
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
        var save = GlobalItemSystem.Load();
        save.GlobalFlags[RandomGlobalFlagID.ToString()] = 1;
        if (CollectableItem.IsAdditive)
        {
            GlobalItemSystem.AddItemCount(save, CollectableItem.ItemName, CollectableItem.ItemCount);
        }
        else
        {
            GlobalItemSystem.SetItemCount(save, CollectableItem.ItemName, CollectableItem.ItemCount);
        }
        save.Save();
    }
}
