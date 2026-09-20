using Godot;

public partial class PlayerUI : Control
{
    public static double CurrentTime = 0;

    [Export]
    public Control MainUI;

    [Export]
    public FinishScreen FinishUI; // Unrelated to Finland.

    public static PlayerUI Instance;

    public override void _EnterTree()
    {
        Instance = this;
        SetFinishMode(false);
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

    public void SetFinishMode(bool finishMode)
    {
        if (finishMode)
        {
            if (MainUI != null)
                MainUI.Visible = false;
            FinishUI.Visible = true;
            FinishUI.ProcessMode = ProcessModeEnum.Inherit;
        }
        else
        {
            if (MainUI != null)
                MainUI.Visible = true;
            FinishUI.Visible = false;
            FinishUI.ProcessMode = ProcessModeEnum.Disabled;
        }
    }
}
