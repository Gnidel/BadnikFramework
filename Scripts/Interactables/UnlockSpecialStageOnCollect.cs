using System;
using Godot;

public partial class UnlockSpecialStageOnCollect : Node
{
    [Export]
    public string StageToUnlock;

    void OnItemCollected(PlayerController collectorPlayer)
    {
        var save = SaveData.Load();
        save.UnlockLevel(StageToUnlock);
        save.Save();
    }
}
