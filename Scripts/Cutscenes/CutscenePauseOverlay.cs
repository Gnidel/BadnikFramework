using Godot;

public partial class CutscenePauseOverlay : Control
{
    [Export]
    public Button ResumeButton;

    [Export]
    public Button SkipButton;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.WhenPaused;
    }
}