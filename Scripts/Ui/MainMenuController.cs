using System;
using System.Collections.Generic;
using Godot;

public partial class MainMenuController : Control
{
    [Export]
    public PackedScene NextLevel;

    [Export]
    public Control MainMenuContainer;

    [Export]
    public Button DefaultMainMenuButton;

    [Export]
    public Control LevelSelectContainer;

    [Export]
    public Button DefaultLevelSelectButton;

    [Export]
    public CharacterSelector CharacterSelector;

    [Export]
    public Button DefaultCharacterSelectorButton;

    [Export]
    public OptionsMenu OptionsMenuContainer;

    [Export]
    public ExtrasMenu ExtrasMenuContainer;

    [Export]
    public LoadingScreen LoadingScreen;

    [Export]
    public SaveSelector SaveSelector;

    [Export]
    public Control SaveSelectorBackground;
    private string CurrentlyLoadingScene;
    private SaveData SaveData;

    [Export]
    public Texture2D RedRingYesIcon;

    [Export]
    public Texture2D RedRingNoIcon;

    // Stage select
    [Export]
    public Node StagesData;

    [Export]
    public Button StageTemplateButton;

    public static MainMenuController Instance;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

    public override void _Ready()
    {
        OptionsMenuContainer.Apply();
        HideAll();
        SaveSelector.Visible = true;
        SaveSelectorBackground.Visible = true;
        LoadingScreen.Visible = false;

        Input.MouseMode = Input.MouseModeEnum.Visible;
        Engine.TimeScale = 1;

        PauseMenu.ExitScene = GetTree().CurrentScene.SceneFilePath;
    }

    public override void _Process(double delta)
    {
        try
        {
            var focus = GetViewport().GuiGetFocusOwner();
            if (focus == null && MainMenuContainer.Visible)
            {
                DefaultMainMenuButton.GrabFocus();
            }

            if (
                !string.IsNullOrEmpty(CurrentlyLoadingScene)
                && LoadingScreen.Returning
                && LoadingScreen.GetLoadingCompletion() >= 1
                && LoadingScreen.Instance.Returning
                && LoadingScreen.Instance.TransitionProcess >= 1
            )
            {
                var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(CurrentlyLoadingScene);
                CurrentlyLoadingScene = null;
                GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
            }
        }
        catch (Exception e)
        {
            ErrorLog.AddMessage(e.Message + e.StackTrace.ToString());
        }
    }

    public void ToMainMenu()
    {
        HideAll();
        MainMenuContainer.Visible = true;
        DefaultMainMenuButton.GrabFocus();
    }

    public void ToSaveSelector()
    {
        HideAll();
        SaveSelector.Visible = true;
        SaveSelectorBackground.Visible = true;
        SaveSelector.Refresh();
    }

    public void SelectSave(int saveId)
    {
        SaveData.SaveFileId = saveId;
        SaveData = SaveData.Load();
        SaveData.Save();
        PopulateLevelSelect();
        ToMainMenu();
    }

    public void ToLevelSelectMenu()
    {
        HideAll();
        //LevelSelectContainer.Visible = true;
        //DefaultLevelSelectButton.GrabFocus();
        CharacterSelector.PreviousMenu = MainMenuContainer;
        CharacterSelector.NextMenu = LevelSelectContainer;
        CharacterSelector.Visible = true;
        CharacterSelector.FocusFirstSelector();
    }

    public void HideAll()
    {
        MainMenuContainer.Visible = false;
        LevelSelectContainer.Visible = false;
        CharacterSelector.Visible = false;
        OptionsMenuContainer.Visible = false;
        ExtrasMenuContainer.Visible = false;
        SaveSelector.Visible = false;
        SaveSelectorBackground.Visible = false;
    }

    public void OnPlay()
    {
        StartLoadingScene(NextLevel?.ResourcePath);
    }

    private void StartLoadingScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            ErrorLog.AddMessage("MainMenuController.OnPlay called without a valid scene path.");
            return;
        }

        HideAll();
        LoadingScreen.Visible = true;
        LoadingScreen.Return();
        CurrentlyLoadingScene = scenePath;

        if (LoadingScreen.StagePathsToStageNames.TryGetValue(scenePath, out var stageName))
        {
            var zoneText = stageName.Item1.ToString().Trim();
            var actText = stageName.Item2.ToString().Trim();
            LoadingScreen.SetTexts(zoneText, actText);
        }
        else
        {
            LoadingScreen.SetTexts("Loading", "");
        }

        if (
            ResourceLoader.LoadThreadedGetStatus(scenePath)
            == ResourceLoader.ThreadLoadStatus.InvalidResource
        )
        {
            List<string> trackedResources = new List<string>();

            trackedResources.Add(scenePath);
            ResourceLoader.LoadThreadedRequest(scenePath);

            foreach (var player in PlayersManager.PlayablePlayers)
            {
                ResourceLoader.LoadThreadedRequest(player.ResourcePath);
                trackedResources.Add(player.ResourcePath);
            }
            foreach (var player in PlayersManager.NpcPlayers)
            {
                ResourceLoader.LoadThreadedRequest(player.ResourcePath);
                trackedResources.Add(player.ResourcePath);
            }
            LoadingScreen.TrackedProgressResourcePaths = trackedResources.ToArray();
        }
    }

    public void OnPlay(string stagePath)
    {
        CheckpointSystem.Reset();
        CheckpointSystem.CheckpointScene = stagePath;
        StartLoadingScene(stagePath);
    }

    public void OnOptions()
    {
        HideAll();
        OptionsMenuContainer.Visible = true;
        OptionsMenuContainer.SetDefaultFocus();
    }

    public void OnExtras()
    {
        HideAll();
        ExtrasMenuContainer.Visible = true;
        ExtrasMenuContainer.SetDefaultFocus();
    }

    public void OnSaveData()
    {
        ToSaveSelector();
    }

    public void OnQuit()
    {
        GetTree().Quit();
    }

    void PopulateLevelSelect()
    {
        foreach (var child in LevelSelectContainer.GetChild(0).GetChildren())
        {
            if (child.Name != "Template" && child.Name != "Back")
                child.QueueFree();
        }

        if (SaveData == null)
            SaveData = SaveData.Load();
        foreach (var stageDataNode in StagesData.GetChildren())
        {
            MainMenuStagesData stage = (MainMenuStagesData)stageDataNode;
            if (!(stage.UnlockedByDefault || SaveData.IsLevelUnlocked(stage.StageUniqueIdentifier)))
                continue;
            var newButton = (BaseButton)StageTemplateButton.Duplicate();
            var stageIcon = newButton.GetNode<TextureRect>("./HBoxContainer/StageIcon");
            if (stageIcon != null && stage.Icon != null)
            {
                stageIcon.Texture = stage.Icon;
            }
            var stageNameLabel = newButton.GetNode<Label>("./HBoxContainer/StageName");
            stageNameLabel.Text = stage.StageName + "\n" + stage.ActName;

            newButton.Pressed += () =>
            {
                this.OnPlay(stage.ScenePath);
            };

            string bestTimeString = "--:--:--";
            string bestRankString = "-";

            string characterName = "Sonic"; // TODO: Use your character

            if (SaveData.LevelSaves.ContainsKey(stage.StageUniqueIdentifier))
            {
                if (
                    SaveData
                        .LevelSaves[stage.StageUniqueIdentifier]
                        .BestTime.ContainsKey(characterName)
                )
                {
                    bestTimeString = TimeSpan
                        .FromSeconds(
                            SaveData.LevelSaves[stage.StageUniqueIdentifier].BestTime[characterName]
                        )
                        .ToString(@"mm\:ss\:ff");
                }

                if (
                    SaveData
                        .LevelSaves[stage.StageUniqueIdentifier]
                        .BestRank.ContainsKey(characterName)
                )
                {
                    bestRankString = SaveData.LevelSaves[stage.StageUniqueIdentifier].BestRank[
                        characterName
                    ];
                }

                if (stage.HasRedRings)
                {
                    newButton.GetNode<Control>("./HBoxContainer/VBoxContainer/RedRings").Visible =
                        true;
                    for (int redring = 1; redring <= 5; redring++)
                    {
                        if (
                            SaveData
                                .LevelSaves[stage.StageUniqueIdentifier]
                                .Items.GetValueOrDefault("RedRing" + redring.ToString()) > 0
                        )
                        {
                            newButton
                                .GetNode<TextureRect>(
                                    "./HBoxContainer/VBoxContainer/RedRings/RedRing"
                                        + redring.ToString()
                                )
                                .Texture = RedRingYesIcon;
                        }
                    }
                }
            }

            if (!stage.HasRedRings)
            {
                newButton.GetNode<Control>("./HBoxContainer/VBoxContainer/RedRings").Visible =
                    false;
            }
            if (stage.HasRanking)
            {
                newButton.GetNode<Label>("./HBoxContainer/VBoxContainer/BestTime").Text =
                    "BEST TIME:   " + bestTimeString;
                newButton.GetNode<Label>("./HBoxContainer/VBoxContainer/BestRank").Text =
                    "BEST RANK:  " + bestRankString;
            }
            else
            {
                newButton.GetNode<Label>("./HBoxContainer/VBoxContainer/BestTime").Visible = false;
                newButton.GetNode<Label>("./HBoxContainer/VBoxContainer/BestRank").Visible = false;
            }
            newButton.Visible = true;
            newButton.ProcessMode = ProcessModeEnum.Inherit;

            LevelSelectContainer.GetChild(0).AddChild(newButton);
            LevelSelectContainer
                .GetChild(0)
                .MoveChild(newButton, LevelSelectContainer.GetChild(0).GetChildCount() - 2);
        }
        StageTemplateButton.Visible = false;
        StageTemplateButton.ProcessMode = ProcessModeEnum.Inherit;
    }
}
