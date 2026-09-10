using System;
using Godot;

public partial class ApplyPlayerSettings : Node
{
    [Export]
    public Control FpsDisplay;

    public override void _Ready()
    {
        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.Far = OptionsMenu.Options.RenderDistance;
        }
        FpsDisplay.Visible = OptionsMenu.Options.ShowFps;
        FpsDisplay.ProcessMode = OptionsMenu.Options.ShowFps
            ? ProcessModeEnum.Inherit
            : ProcessModeEnum.Disabled;
    }
}
