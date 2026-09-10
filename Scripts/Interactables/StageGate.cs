using System;
using System.Collections.Generic;
using Godot;

public partial class StageGate : Node3D
{
    [Export]
    public string StageName;

    [Export]
    public string StageActName;

    [Export]
    public string StageUniqueIdentifier;

    [Export]
    public bool UnlockedByDefault = false;

    [Export]
    public Texture2D UnlockedImage;

    [Export]
    public Texture2D LockedImage;

    [Export]
    public MapUIIconTrackedObject MapIcon;

    [Export]
    public Control StageEntryScreen { get; set; }

    [Export]
    public RichTextLabel StageLabel { get; set; }

    [Export]
    public RichTextLabel ActLabel { get; set; }

    [Export]
    public Label3D StageNameLabel3D { get; set; }

    [Export]
    public TextureRect StageImage;

    [Export]
    public Button[] DefaultFocusOrder;

    [Export]
    public RichTextLabel BestTimesLabel;

    [Export]
    public string Animation = "Idle";

    [Export]
    public string NextStagePath;

    [Export]
    public float ScaleSpeed = 3;

    [Export]
    public Button PlayButton;

    [Export]
    public Button UnlockButton;
    private SaveData SaveData;

    [Export]
    public bool UnlockedWithItems = false;

    [Export]
    public Godot.Collections.Array<RequiredItem> RequiredItems =
        new Godot.Collections.Array<RequiredItem>();

    [Export]
    public bool ConsumeItems = false;

    bool waitingForLoading = false;

    void UpdateStageNameLabels()
    {
        if (isUnlocked())
        {
            if (StageLabel != null)
            {
                StageLabel.Text = StageName;
            }
            if (ActLabel != null)
            {
                ActLabel.Text = StageActName;
            }
            if (StageNameLabel3D != null)
            {
                StageNameLabel3D.Text = StageName + "\n" + StageActName;
            }
            if (MapIcon != null)
            {
                MapIcon.Description = StageName + "\n" + StageActName + "\n";
            }
            PlayButton.Visible = true;
            UnlockButton.Visible = false;
        }
        else
        {
            if (StageLabel != null)
            {
                StageLabel.Text = "???";
            }
            if (ActLabel != null)
            {
                ActLabel.Text = "???";
            }
            if (StageNameLabel3D != null)
            {
                StageNameLabel3D.Text = "???";
            }
            if (MapIcon != null)
            {
                MapIcon.Description += "???\n";
            }
            PlayButton.Visible = false;
            UnlockButton.Visible = UnlockedWithItems;
            if (UnlockedWithItems)
            {
                UnlockButton.Disabled = !CanUnlock();
            }
        }
    }

    public override void _Ready()
    {
        StageEntryScreen.Visible = false;
        UpdateStageNameLabels();
        populateBestTimes();

        SaveData = SaveData.Load();
    }

    public override void _EnterTree()
    {
        if (MapUIControl.Instance != null)
        {
            MapUIControl.Instance.StageLocations.Add(this.GlobalPosition);
            MapUIControl.Instance.RefreshIcons();
        }
    }

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player == null)
            return;

        player.LinearVelocity = player.LinearVelocity.ProjectOnPlane(player.Gravity);

        OpenStageMenu();
    }

    public override void _Process(double delta)
    {
        if (waitingForLoading) // TODO: This is copypasta from FinishScreen. Refactor this to use shared code.
        {
            if (
                ResourceLoader.LoadThreadedGetStatus(NextStagePath)
                    == ResourceLoader.ThreadLoadStatus.Loaded
                && LoadingScreen.Instance.Returning
                && LoadingScreen.Instance.TransitionProcess >= 1
            )
            {
                if (StageData.Instance.Style != StageData.StageStyle.SpecialStage)
                {
                    CheckpointSystem.Reset();
                }
                var nextScene = CheckpointSystem.CheckpointScene;

                if (NextStagePath == "res://" || NextStagePath == "" || NextStagePath == null)
                {
                    nextScene = ProjectSettings.GetSetting("application/run/main_scene").AsString();
                    GetTree().ChangeSceneToFile(nextScene);
                }
                else
                {
                    var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(NextStagePath);
                    GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
                }
            }
            else
            {
                StageEntryScreen.Scale = (
                    StageEntryScreen.Scale - Vector2.One * (float)delta * ScaleSpeed
                ).Clamp(Vector2.Zero, Vector2.One);
            }
            return;
        }
        StageEntryScreen.Scale = (
            StageEntryScreen.Scale + Vector2.One * (float)delta * ScaleSpeed
        ).Clamp(Vector2.Zero, Vector2.One);
    }

    //public void OnBodyExit(Node3D other)
    //{
    //    var player = other.GetNodeOrNull<PlayerController>(".");
    //    if (player == null) return;

    //    CloseStageMenu();
    //}

    bool isUnlocked()
    {
        if (SaveData == null)
            SaveData = SaveData.Load();
        return (UnlockedByDefault || SaveData.IsLevelUnlocked(StageUniqueIdentifier));
    }

    void populateBestTimes(string CharacterName = "Sonic") // TODO: Support for more characters
    {
        if (SaveData == null)
            SaveData = SaveData.Load();
        var stageSave = SaveData.LevelSaves.GetValueOrDefault(StageUniqueIdentifier, null);
        if ((stageSave == null || !stageSave.Unlocked) && UnlockedWithItems)
        {
            BestTimesLabel.Text = "Required:\n";
            foreach (var item in RequiredItems)
            {
                var allPlayersItemSum = 0;
                foreach (var player in PlayerController.Instances)
                {
                    allPlayersItemSum += player.PlayerInventory.GetItemCount(item.Id);
                }
                BestTimesLabel.Text +=
                    "[img=32x32]"
                    + item.Icon.ResourcePath
                    + "[/img]"
                    + ": "
                    + allPlayersItemSum
                    + "\\"
                    + item.Count
                    + "\n";
            }
        }
        else
        {
            if (
                stageSave == null
                || !stageSave.Unlocked
                || !stageSave.BestTime.ContainsKey(CharacterName)
                || !stageSave.BestRank.ContainsKey(CharacterName)
            )
            {
                BestTimesLabel.Text = "? --:--:--";
            }
            else
            {
                int minutes = (int)(stageSave.BestTime[CharacterName] / 60);
                int seconds = (int)(stageSave.BestTime[CharacterName]) % 60;
                int hect = (int)(stageSave.BestTime[CharacterName] * 100) % 100;

                string timeString =
                    minutes.ToString("D2")
                    + ":"
                    + seconds.ToString("D2")
                    + ":"
                    + hect.ToString("D2");
                BestTimesLabel.Text = stageSave.BestRank[CharacterName] + " " + timeString;

                if (MapIcon != null)
                {
                    MapIcon.Description +=
                        "\n\nBest rank: " + stageSave.BestRank[CharacterName] + "\n\n";
                    MapIcon.Description += "Best time: " + timeString + "\n";
                }
            }
        }
    }

    void OpenStageMenu()
    {
        PlayButton.Disabled = !isUnlocked();
        StageImage.Texture = isUnlocked() ? UnlockedImage : LockedImage;
        UpdateStageNameLabels();
        populateBestTimes();

        StageEntryScreen.Visible = true;
        StageEntryScreen.ProcessMode = ProcessModeEnum.Inherit;
        foreach (var focus in DefaultFocusOrder)
        {
            if (focus.Visible && !focus.Disabled)
            {
                focus.GrabFocus();
                break;
            }
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;

        LockPlayers();
        StageEntryScreen.Scale = Vector2.Zero;
    }

    void CloseStageMenu()
    {
        UnlockPlayers();
        StageEntryScreen.Visible = false;
        StageEntryScreen.ProcessMode = ProcessModeEnum.Disabled;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void LockPlayers()
    {
        foreach (var player in PlayerController.Instances)
        {
            var actionAutoGimmick = player.GetNodeOrNull<ActionAutoGimmick>(
                "./PlayerControl/Actions/ActionAutoGimmick"
            );
            if (actionAutoGimmick != null)
            {
                actionAutoGimmick.StartAutoGimmick(Animation, null, true);
                //player.Grounded = true;
            }
        }
    }

    private void UnlockPlayers()
    {
        foreach (var player in PlayerController.Instances)
        {
            var actionAutoGimmick = player.GetNodeOrNull<ActionAutoGimmick>(
                "./PlayerControl/Actions/ActionAutoGimmick"
            );
            if (actionAutoGimmick != null)
            {
                actionAutoGimmick.EndAutoGimmick("Idle");
            }
        }
    }

    void OnPlay()
    {
        waitingForLoading = true;
        StageData.Instance.IsFinished = true;
        LoadingScreen.Instance.ColorRect.Color = new Color(0, 0, 0, 1);
        LoadingScreen.InitialColor = new Color(0, 0, 0, 1);
        LoadingScreen.Instance.Return();
        var nextScene = NextStagePath;
        if (NextStagePath == "res://" || NextStagePath == "" || NextStagePath == null)
        {
            nextScene = ProjectSettings.GetSetting("application/run/main_scene").AsString();
        }
        CheckpointSystem.Reset();
        CheckpointSystem.CheckpointScene = nextScene;
        LoadingScreen.Instance.SetTexts(StageName, StageActName);

        if (
            ResourceLoader.LoadThreadedGetStatus(nextScene)
            == ResourceLoader.ThreadLoadStatus.InvalidResource
        )
        {
            ResourceLoader.LoadThreadedRequest(nextScene);
        }
    }

    public bool CanUnlock()
    {
        var remainingItems = new Dictionary<string, int>();
        foreach (var item in RequiredItems)
        {
            remainingItems[item.Id] = item.Count;
        }

        foreach (var player in PlayerController.Instances)
        {
            var playerInventory = player.PlayerInventory;
            foreach (var item in remainingItems)
            {
                remainingItems[item.Key] -= playerInventory.GetItemCount(item.Key);
            }
        }

        foreach (var remainingItem in remainingItems)
        {
            if (remainingItem.Value > 0)
                return false;
        }
        return true;
    }

    public void OnUnlock()
    {
        // Note: It won't be fair. It will consume items from players in whatever order.
        // RISKY ASSUMPTION THAT THE REQUIRED ITEMS ARE GLOBAL!
        if (!CanUnlock())
            return;
        SaveData = SaveData.Load();

        var remainingItems = new Dictionary<string, int>();
        foreach (var item in RequiredItems)
        {
            remainingItems[item.Id] = item.Count;
            if (ConsumeItems)
            {
                SaveData.GlobalItems[item.Id] = 0;
            }
        }

        foreach (var player in PlayerController.Instances)
        {
            var playerInventory = player.PlayerInventory;
            foreach (var item in remainingItems)
            {
                var currentPlayerItemCount = playerInventory.GetItemCount(item.Key);
                var itemsToRemove = Math.Min(item.Value, currentPlayerItemCount);
                remainingItems[item.Key] -= itemsToRemove;
                if (ConsumeItems)
                {
                    playerInventory.SetItemCount(item.Key, currentPlayerItemCount - itemsToRemove);
                    SaveData.GlobalItems[item.Key] += playerInventory.GetItemCount(item.Key);
                }
            }
        }

        SaveData.UnlockLevel(StageUniqueIdentifier);

        SaveData.Save();
        MapUIControl.Instance.RefreshIcons();
        OpenStageMenu();
    }
}
