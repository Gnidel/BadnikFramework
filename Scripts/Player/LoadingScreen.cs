using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class LoadingScreen : Control
{
    public static LoadingScreen Instance;
    public float TransitionProcess;
    public float TextTransitionProcess;

    [Export]
    public float TransitionProcessSpeed = 1f;

    [Export]
    public float TextTransitionProcessSpeed = 1f;

    [Export]
    public ColorRect ColorRect;

    [Export]
    public Color TargetColor = new Color(0, 0, 0, 1);
    public static Color InitialColor = new Color(0, 0, 0, 1);

    [Export]
    public RichTextLabel ZoneNameLabel;

    [Export]
    public RichTextLabel ActNameLabel;

    [Export]
    public Control TextCard;

    [Export]
    public ProgressBar ProgressBar;
    public static string[] TrackedProgressResourcePaths = new string[0];

    public bool Returning;
    private string PendingScenePath;
    private Vector2 InitialPosition;
    private Vector2 LeavePosition;

    private Vector2 TextInitialPosition;
    private Vector2 TextLeavePosition;

    // // // //
    [Export]
    Control[] Movables = new Control[0];
    private Vector2[] MovableInitialPositions = new Vector2[0];

    [Export]
    public Vector2[] MovableLeavePositions = new Vector2[0];

    [Export]
    public float[] MovableTransitionSpeeds = new float[0];
    private float[] MovableTransitionProcess = new float[0];
    private int CurrentMovable = 0;

    public static string ZoneName = "Loading";
    public static string ActName = "";
    public static Dictionary<string, Tuple<string, string>> StagePathsToStageNames =
        new Dictionary<string, Tuple<string, string>>(); // Key: scene path, value: tuple of <zone name, act name>

    public override void _EnterTree()
    {
        Instance = this;
        this.Visible = true;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Ready()
    {
        InitialPosition = ColorRect.Position;
        LeavePosition = ColorRect.Position + new Vector2(-5000, 0);
        ColorRect.Color = InitialColor;

        TextInitialPosition = TextCard.Position;
        TextLeavePosition = TextCard.Position + new Vector2(+5000, 0);

        MovableInitialPositions = new Vector2[Movables.Length];
        MovableTransitionProcess = new float[Movables.Length];
        for (int i = 0; i < Movables.Length; i++)
        {
            MovableInitialPositions[i] = Movables[i].Position;
            MovableTransitionProcess[i] = 0;
        }

        SetTexts();
    }

    public override void _Process(double delta)
    {
        UpdateProgressBar();
        if (
            CurrentMovable < 0
            || CurrentMovable >= MovableTransitionProcess.Length
            || CurrentMovable >= MovableTransitionSpeeds.Length
            || CurrentMovable >= Movables.Length
        )
            return;

        if (!Returning)
        {
            MovableTransitionProcess[CurrentMovable] +=
                (float)delta * MovableTransitionSpeeds[CurrentMovable];
            if (MovableTransitionProcess[CurrentMovable] >= 1)
            {
                MovableTransitionProcess[CurrentMovable] = 1;
            }

            Movables[CurrentMovable].Position = MovableInitialPositions[CurrentMovable]
                .Lerp(
                    MovableLeavePositions[CurrentMovable],
                    MovableTransitionProcess[CurrentMovable]
                );

            if (MovableTransitionProcess[CurrentMovable] >= 1)
            {
                CurrentMovable++;
                if (CurrentMovable >= Movables.Length)
                {
                    this.Visible = false;
                    this.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
        else
        {
            MovableTransitionProcess[CurrentMovable] +=
                (float)delta * MovableTransitionSpeeds[CurrentMovable];
            if (MovableTransitionProcess[CurrentMovable] >= 1)
            {
                MovableTransitionProcess[CurrentMovable] = 1;
            }

            Movables[CurrentMovable].Position = MovableLeavePositions[CurrentMovable]
                .Lerp(
                    MovableInitialPositions[CurrentMovable],
                    MovableTransitionProcess[CurrentMovable]
                );

            if (MovableTransitionProcess[CurrentMovable] >= 1)
            {
                CurrentMovable++;
                if (CurrentMovable >= Movables.Length)
                {
                    ProgressBar.Value = 100f;
                    //this.ProcessMode = ProcessModeEnum.Disabled;
                    TransitionProcess = 1;
                    LoadingScreen.InitialColor = new Color(0, 0, 0, 1); // Black
                    if (!string.IsNullOrEmpty(PendingScenePath))
                    {
                        var scenePath = PendingScenePath;
                        PendingScenePath = null;
                        GetTree().ChangeSceneToFile(scenePath);
                    }
                }
            }
        }
    }

    public void ChangeSceneAfterTransition(string scenePath, bool showStageNames = true)
    {
        PendingScenePath = scenePath;
        ProcessMode = ProcessModeEnum.Inherit;
        Return();
        if (!showStageNames)
        {
            SetTexts("", "");
        }
    }

    public void ReloadSceneAfterTransition()
    {
        ChangeSceneAfterTransition(GetTree().CurrentScene.SceneFilePath);
    }

    public double GetLoadingCompletion()
    {
        var progress = new Godot.Collections.Array();
        double combinedProcess = 0;
        foreach (var track in TrackedProgressResourcePaths)
        {
            var status = ResourceLoader.LoadThreadedGetStatus(track, progress);
            switch (status)
            {
                case ResourceLoader.ThreadLoadStatus.InProgress:
                    combinedProcess += progress[0].AsDouble();
                    break;
                case ResourceLoader.ThreadLoadStatus.Loaded:
                    combinedProcess += 1;
                    break;
                case ResourceLoader.ThreadLoadStatus.Failed:
                    ZoneNameLabel.Text = "Error";
                    ActNameLabel.Text = "";
                    break;
                case ResourceLoader.ThreadLoadStatus.InvalidResource: // Happens with scene change. 1 looks prettier.
                    combinedProcess += 1;
                    break;
            }
        }
        return combinedProcess / TrackedProgressResourcePaths.Length;
    }

    void UpdateProgressBar()
    {
        if (TrackedProgressResourcePaths.Length > 0)
        {
            ProgressBar.Value = GetLoadingCompletion() * 100;
        }
        else
        {
            ProgressBar.Value = 100f;
        }
    }

    public void Return()
    {
        Returning = true;
        this.Visible = true;
        this.ProcessMode = ProcessModeEnum.Inherit;
        //TransitionProcess = 0;
        //TextTransitionProcess = 0;

        for (int i = 0; i < Movables.Length; i++)
        {
            MovableTransitionProcess[i] = 0;
            CurrentMovable = 0;
        }

        SetTexts();
    }

    public void SetTexts()
    {
        string zoneText;
        string actText;
        if (StageData.Instance != null && !StageData.Instance.IsFinished)
        {
            zoneText = StageData.Instance.StageZoneName;
            actText = StageData.Instance.StageActName;
        }
        else if (
            FinishScreen.NextStagePath != null
            && FinishScreen.NextStagePath != ""
            && LoadingScreen.StagePathsToStageNames.ContainsKey(FinishScreen.NextStagePath)
        )
        {
            zoneText = LoadingScreen
                .StagePathsToStageNames[FinishScreen.NextStagePath]
                .Item1.ToString()
                .Trim();
            actText = LoadingScreen
                .StagePathsToStageNames[FinishScreen.NextStagePath]
                .Item2.ToString()
                .Trim();
        }
        else
        {
            zoneText = "Loading";
            actText = "";
        }
        ZoneNameLabel.Text = "[center]" + zoneText + "[/center]";
        ActNameLabel.Text = "[center]" + actText + "[/center]";
    }

    public void SetTexts(string zoneText, string actText)
    {
        ZoneNameLabel.Text = "[center]" + zoneText + "[/center]";
        ActNameLabel.Text = "[center]" + actText + "[/center]";
    }
}
