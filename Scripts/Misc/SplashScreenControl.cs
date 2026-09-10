using System;
using Godot;

public partial class SplashScreenControl : Node
{
    [Export]
    public float ShowTime = 5;

    [Export]
    public string NextScene;

    [Export]
    public CanvasItem Content;

    private float Timer;
    private float LastAlpha = -1;

    public override void _Ready()
    {
        ResourceLoader.LoadThreadedRequest(NextScene);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsPressed())
        {
            Timer = Mathf.Max(ShowTime, Timer);
        }
    }

    public override void _Process(double delta)
    {
        Timer += (float)delta;
        if (
            Timer > ShowTime
            && ResourceLoader.LoadThreadedGetStatus(NextScene)
                == ResourceLoader.ThreadLoadStatus.Loaded
        )
        {
            var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(NextScene);
            GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
        }

        if (Content == null)
            return;

        var alpha = 1f;
        if (Timer < 1)
        {
            alpha = Timer;
        }
        else if (Timer > ShowTime - 1)
        {
            alpha = ShowTime - Timer;
        }

        alpha = Mathf.Clamp(alpha, 0, 1);
        if (Mathf.IsEqualApprox(alpha, LastAlpha))
            return;

        var modulate = Content.Modulate;
        modulate.A = alpha;
        Content.Modulate = modulate;
        LastAlpha = alpha;
    }
}
