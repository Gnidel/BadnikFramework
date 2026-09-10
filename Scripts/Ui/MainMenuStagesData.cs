using System;
using Godot;

public partial class MainMenuStagesData : Node
{
    [Export]
    public string StageUniqueIdentifier;

    [Export]
    public Texture2D Icon;

    [Export(PropertyHint.MultilineText)]
    public string StageName = "";

    [Export(PropertyHint.MultilineText)]
    public string ActName = "";

    [Export]
    public string ScenePath;

    [Export]
    public bool UnlockedByDefault;

    [Export]
    public bool HasRedRings = false;

    [Export]
    public bool HasRanking = true;

    [Export]
    public StageData.StageStyle StageStyle = StageData.StageStyle.ActionStage;

    public override void _Ready()
    {
        LoadingScreen.StagePathsToStageNames[ScenePath] = new Tuple<string, string>(
            StageName.ToString(),
            ActName.ToString()
        ); // It may look dumb using ToString() on string, but it solves an issue of empty names
    }
}
