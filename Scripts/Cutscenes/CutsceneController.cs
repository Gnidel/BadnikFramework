using Godot;

public partial class CutsceneController : Node
{
    [Signal]
    public delegate void CutsceneFinishedEventHandler(bool skipped);

    [Export]
    public VideoStreamPlayer VideoPlayer;

    [Export]
    public Control PauseOverlay;

    [Export]
    public Button ResumeButton;

    [Export]
    public Button SkipButton;

    [Export]
    public string NextScene = "";

    [Export]
    public bool PlayVideoOnReady = true;

    private bool isPaused;
    private bool isFinished;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        if (VideoPlayer != null)
        {
            VideoPlayer.Finished += OnVideoFinished;
        }

        if (ResumeButton != null)
        {
            ResumeButton.Pressed += Resume;
        }

        if (SkipButton != null)
        {
            SkipButton.Pressed += Skip;
        }

        SetPauseOverlayVisible(false);

        if (VideoPlayer != null && VideoPlayer.Stream != null && PlayVideoOnReady)
        {
            VideoPlayer.Play();
        }
    }

    public override void _ExitTree()
    {
        if (VideoPlayer != null)
        {
            VideoPlayer.Finished -= OnVideoFinished;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (isFinished || !@event.IsActionPressed("any_pause"))
        {
            return;
        }

        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }

        GetViewport().SetInputAsHandled();
    }

    public void Pause()
    {
        if (isFinished || isPaused)
        {
            return;
        }

        isPaused = true;
        GetTree().Paused = true;
        SetPauseOverlayVisible(true);
        ResumeButton?.GrabFocus();
    }

    public void Resume()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        GetTree().Paused = false;
        SetPauseOverlayVisible(false);
    }

    public void Skip()
    {
        Complete(true);
    }

    public void Complete()
    {
        Complete(false);
    }

    // Real-time cutscene controllers can call this when their timeline ends.
    public void Complete(bool skipped = false)
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;
        Resume();
        SetPauseOverlayVisible(false);
        EmitSignal("CutsceneFinished", skipped);

        if (!string.IsNullOrEmpty(NextScene))
        {
            ChangeSceneWithLoading(NextScene);
        }
    }

    private void OnVideoFinished()
    {
        Complete();
    }

    private void SetPauseOverlayVisible(bool visible)
    {
        if (PauseOverlay == null)
        {
            return;
        }

        PauseOverlay.Visible = visible;
        PauseOverlay.ProcessMode = visible
            ? ProcessModeEnum.WhenPaused
            : ProcessModeEnum.Disabled;
    }

    private async void ChangeSceneWithLoading(string scenePath)
    {
        GetTree().Paused = false;
        Error requestError = ResourceLoader.LoadThreadedRequest(scenePath);
        if (requestError != Error.Ok)
        {
            GD.PushError($"Could not load cutscene destination '{scenePath}': {requestError}");
            return;
        }

        if (LoadingScreen.Instance != null)
        {
            LoadingScreen.TrackedProgressResourcePaths = [scenePath];
            LoadingScreen.Instance.Return();
        }

        while (ResourceLoader.LoadThreadedGetStatus(scenePath) != ResourceLoader.ThreadLoadStatus.Loaded)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var packedScene = ResourceLoader.LoadThreadedGet(scenePath) as PackedScene;
        if (packedScene == null)
        {
            GD.PushError($"Cutscene destination '{scenePath}' is not a PackedScene.");
            return;
        }

        GetTree().ChangeSceneToPacked(packedScene);
    }
}