using System;
using Godot;

public partial class AutoPathFollow3D : PathFollow3D
{
    public enum Mode
    {
        FORWARD,
        BACKWARD,
        PING_PONG,
    }

    [Export]
    public float Speed = 1;

    [Export]
    public Mode MoveMode;
    private float dir = 1;
    private Path3D path;

    public Vector3 lastFrameMovement = Vector3.Zero;

    public override void _Ready()
    {
        path = this.GetNode<Path3D>("..");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (MoveMode == Mode.FORWARD)
        {
            dir = 1;
        }
        else if (MoveMode == Mode.BACKWARD)
        {
            dir = -1;
        }
        var nextProgress = this.Progress + dir * Speed * delta;
        var nextProgresssRatio =
            this.ProgressRatio + (dir * Speed * delta / path.Curve.GetBakedLength());

        var lastFramePos = this.GlobalPosition;

        if (MoveMode == Mode.PING_PONG)
        {
            if (dir == 1 && nextProgresssRatio >= 1)
            {
                dir = -1;
                this.ProgressRatio = 1;
                lastFrameMovement = this.GlobalPosition - lastFramePos;
                return;
            }
            else if (dir == -1 && nextProgresssRatio <= 0)
            {
                dir = 1;
                this.ProgressRatio = 0;
                lastFrameMovement = this.GlobalPosition - lastFramePos;
                return;
            }
        }

        this.Progress = (float)nextProgress;
        lastFrameMovement = this.GlobalPosition - lastFramePos;
    }
}
