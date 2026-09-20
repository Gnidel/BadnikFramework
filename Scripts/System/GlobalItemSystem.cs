using System;

public static class GlobalItemSystem
{
    private static SaveData cachedSave;
    private static int cachedSaveFileId = -1;

    public static SaveData Load()
    {
        if (cachedSave == null || cachedSaveFileId != SaveData.SaveFileId)
        {
            cachedSave = SaveData.Load();
            cachedSaveFileId = SaveData.SaveFileId;
        }
        return cachedSave;
    }

    public static int GetItemCount(SaveData save, string itemName)
    {
        return save.GlobalItems.TryGetValue(itemName, out var count) ? count : 0;
    }

    public static int GetItemCount(string itemName)
    {
        return GetItemCount(Load(), itemName);
    }

    public static void AddItemCount(SaveData save, string itemName, int count)
    {
        SetItemCount(save, itemName, GetItemCount(save, itemName) + count);
    }

    public static void SetItemCount(SaveData save, string itemName, int count)
    {
        save.GlobalItems[itemName] = Math.Max(0, count);
    }
}
