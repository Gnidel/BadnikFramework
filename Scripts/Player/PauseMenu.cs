using System;
using Godot;

public partial class PauseMenu : Control
{
	[Export]
	public Control MenuContainer;

	[Export]
	public Control RestartMenuContainer;

	[Export]
	public Control RestartCheckpointButton;

	[Export]
	public Control RestartLevelButton;

	[Export]
	public OptionsMenu OptionsMenuContainer;

	[Export]
	public Control RestartBackButton;

	[Export]
	public Control DefaultFocus;

	[Export]
	public Control RestartMenuButton;

	[Export]
	public Control MapButton;

	//[Export] public PackedScene ExitScene;
	public static string ExitScene = "";

	const string inputName = "any_pause";
	bool IsPaused = false;

	public static PauseMenu Instance;

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
		HideAll();
		if (StageData.Instance.Style != StageData.StageStyle.ActionStage)
		{
			RestartMenuButton.Visible = false;
			RestartMenuButton.ProcessMode = ProcessModeEnum.Disabled;
		}
		if (MapUIControl.Instance == null)
		{
			MapButton.Visible = false;
			MapButton.ProcessMode = ProcessModeEnum.Disabled;
		}

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed(inputName)) // Not using PlayerInput here because it should work with zero TimeScale and it's independent of players
        {
            IsPaused = !IsPaused;
            if (!IsPaused)
            {
                Unpause();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Unpause()
    {
        IsPaused = false;
        //Engine.TimeScale = 1;
        GetTree().Paused = false;
        HideAll();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void HideAll()
    {
        MenuContainer.Visible = false;
        RestartMenuContainer.Visible = false;
        OptionsMenuContainer.Visible = false;

        if (MapUIControl.Instance != null)
        {
            MapUIControl.Instance.ProcessMode = ProcessModeEnum.Disabled;
            MapUIControl.Instance.MapUI.Visible = false;
        }
    }

    public void Pause()
    {
        IsPaused = true;
        GetTree().Paused = true;
        HideAll();
        MenuContainer.Visible = true;
        DefaultFocus.GrabFocus();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Restart()
    {
        HideAll();
        RestartMenuContainer.Visible = true;
        RestartCheckpointButton.Visible = CheckpointSystem.CheckpointPos.HasValue;
        if (RestartCheckpointButton.Visible)
        {
            RestartCheckpointButton.GrabFocus();
        }
        else
        {
            RestartLevelButton.GrabFocus();
        }
        RestartBackButton.Visible = PlayerDamage.AlivePlayersCount() > 0;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Engine.TimeScale = 1;
    }

    public void Exit()
    {
        Unpause();
        if (
            StageData.Instance.ExitSceneOverride != null
            && StageData.Instance.ExitSceneOverride.Length > 0
        )
        {
            ExitScene = StageData.Instance.ExitSceneOverride;
        }
        if (ExitScene == "" || ExitScene == null)
        {
            ExitScene = ProjectSettings.GetSetting("application/run/main_scene").AsString();
        }
        
        LoadingScreen.Instance.ChangeSceneAfterTransition(ExitScene, false);
    }

    public void RestartMenuYes()
    {
        CheckpointSystem.Reset();
        Unpause();
        Engine.TimeScale = 1;
        LoadingScreen.Instance.ReloadSceneAfterTransition();
    }

    public void RestartMenuCheckpoint()
    {
        if (!CheckpointSystem.CheckpointPos.HasValue)
        {
            RestartMenuYes();
            return;
        }

        Unpause();
        Engine.TimeScale = 1;

        LoadingScreen.Instance.ReloadSceneAfterTransition();
    }

    public void RestartMenuNo()
    {
        HideAll();
        Pause();
    }

    public void Options()
    {
        HideAll();
        OptionsMenuContainer.Visible = true;
        OptionsMenuContainer.SetDefaultFocus();
    }

    public void OnMapButton()
    {
        HideAll();
        MapUIControl.Instance.ProcessMode = ProcessModeEnum.WhenPaused;
        MapUIControl.Instance.RefreshIcons();
        MapUIControl.Instance.MapUI.Visible = true;
    }
}
