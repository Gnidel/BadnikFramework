using System;
using Godot;

public partial class DebugUI : RichTextLabel
{
    [Export]
    public bool ShowDebugInfo = false;
    private double fpsTimer = 0f;
    private double minFps = Mathf.Inf;
    private double maxFps = 0f;

    public override void _Process(double delta)
    {
        var fps = Engine.GetFramesPerSecond();
        this.Text = "";

        if (ShowDebugInfo)
        {
            foreach (var player in PlayerController.Instances)
            {
                this.Text +=
                    "\nVelocity: "
                    + player.LinearVelocity
                    + "\nVelocityXZ: "
                    + player.VelocityXZ.Length();
            }
        }
        this.Text += "\nFPS: " + fps;

        fpsTimer += delta;
        if (fpsTimer > 5)
        {
            fpsTimer -= 5;
            minFps = fps;
            maxFps = fps;
        }
        else
        {
            if (minFps > fps)
                minFps = fps;
            if (maxFps < fps)
                maxFps = fps;
        }

        this.Text += "\nMin FPS: " + minFps;
        this.Text += "\nMax FPS: " + maxFps;
    }
}
