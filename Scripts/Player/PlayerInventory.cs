using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

public partial class PlayerInventory : Node
{
    //[Export] public Godot.Collections.Dictionary Items = new Godot.Collections.Dictionary();
    [JsonInclude]
    public System.Collections.Generic.Dictionary<string, int> Items =
        new System.Collections.Generic.Dictionary<string, int>();
    public PlayerController Player;

    public override void _Ready()
    {
        if (Player == null)
        {
            Player = this.GetNode<PlayerController>("..");
        }

        if (!CheckpointSystem.CheckpointItems.ContainsKey(Player.PlayerID))
        {
            CheckpointSystem.CheckpointItems.Add(Player.PlayerID, new Dictionary<string, int>());
        }

        if (StageData.Instance.Style != StageData.StageStyle.SpecialStage)
        {
            if (CheckpointSystem.CheckpointPos != null)
            {
                ItemsFromCheckpoint();
            }
            else
            {
                LoadItems();
            }
        }
    }

    void LoadItems()
    {
        Items = new System.Collections.Generic.Dictionary<string, int>();

        var save = SaveData.Load();
        // Stage items
        foreach (
            var item in save
                .LevelSaves.GetValueOrDefault(
                    StageData.Instance.StageUniqueIdentifier,
                    new SaveData.LevelSave()
                )
                .Items
        )
        {
            AddItemCount(item.Key, item.Value);
        }
        // Global items
        foreach (var item in save.GlobalItems)
        {
            AddItemCount(item.Key, item.Value);
        }
    }

    public void ItemsFromCheckpoint()
    {
        // Checkpoint items
        Items.Clear();
        foreach (var item in CheckpointSystem.CheckpointItems[Player.PlayerID])
        {
            SetItemCount(item.Key, item.Value);
        }
    }

    public void SetItemCount(string itemType, int count, bool emitSignal = false)
    {
        var prevCount = Items.GetValueOrDefault(itemType, 0);
        Items[itemType] = count;
        if (emitSignal)
        {
            EmitSignal(SignalName.OnItemCollected, itemType, count, count - prevCount);
        }
    }

    public int GetItemCount(string itemType)
    {
        var containsKey = Items.ContainsKey(itemType);
        if (!containsKey)
        {
            SetItemCount(itemType, 0);
        }
        return Items[itemType];
    }

    public void AddItemCount(string itemType, int count, bool emitSignal = false)
    {
        var prevCount = GetItemCount(itemType);
        SetItemCount(itemType, prevCount + count);
        if (emitSignal)
        {
            EmitSignal(SignalName.OnItemCollected, itemType, prevCount + count, count);
        }
    }

    [Signal]
    public delegate void OnItemCollectedEventHandler(string itemType, int newCount, int difference);
}
