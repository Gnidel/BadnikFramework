using System;
using Godot;

[Tool]
public partial class Spline2DTrigger : Area3D
{
    [Export]
    public Path3D Path;

    [Export]
    public bool ReleaseOnJump = false;

    [Export]
    public bool TwoDimensionalMode = false;

    [Export]
    public bool ReleaseOnExit; // Note: Keep in mind it doesn't work with ConcavePolygonShape.

    [Export]
    public float ReleaseOnDistanceFromPath = -1; // This is a backup for ConcavePolygonShape.

    [Export]
    public bool ReleaseOnEnds = false;

    [Export]
    public bool SpeedLimit = false;

    [Export]
    public float MinSpeedLimit = 50f;

    [Export]
    public float MaxSpeedLimit = 50f;

    [Export]
    public bool LockedInput = false;

    [Export]
    public bool AggressiveDepthCorrection = false; // Basically teleport. Use it for smaller 2D loops or tight places.

    [Export]
    public Spline2DTrigger OnExitTrigger; // Use this in case you have trigger inside trigger, for example a corkscrew in 2D sections

    public override void _Ready()
    {
        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.BodyEntered += this.OnBodyEnter;
        }

        if (GetSignalConnectionList("body_exited").Count == 0)
        {
            this.BodyExited += this.OnBodyExit;
        }
    }

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            var splineMovement = other.GetNodeOrNull<SplineMovement>(
                "./PlayerControl/SplineMovement"
            );
            if (splineMovement != null)
            {
                splineMovement.Path = this.Path;
                splineMovement.WasGrounded = player.Grounded;
                splineMovement.ReleaseOnJump = this.ReleaseOnJump;
                splineMovement.ReleaseOnEnds = this.ReleaseOnEnds;
                splineMovement.ReleaseOnDistanceFromPath = this.ReleaseOnDistanceFromPath;
                splineMovement.SpeedLimit = this.SpeedLimit;
                if (this.SpeedLimit)
                {
                    splineMovement.MinSpeedLimit = this.MinSpeedLimit;
                    splineMovement.MaxSpeedLimit = this.MaxSpeedLimit;
                }
                splineMovement.AggressiveDepthCorrection = AggressiveDepthCorrection;
                splineMovement.Trigger = this;

                player.PlayerInput.TwodimensionalMode = TwoDimensionalMode;
            }
        }
        else
        {
            for (int i = 0; i < other.GetChildCount(); i++)
            {
                var npsm = other.GetChild(i).GetNodeOrNull<NonPlayerSplineMovement>(".");
                if (npsm != null)
                {
                    npsm.Path = this.Path;
                }
            }
        }
    }

    public void OnBodyExit(Node3D other)
    {
        if (!ReleaseOnExit)
            return;

        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            var splineMovement = other.GetNodeOrNull<SplineMovement>(
                "./PlayerControl/SplineMovement"
            );
            if (splineMovement != null)
            {
                if (OnExitTrigger != null)
                {
                    OnExitTrigger.OnBodyEnter(other);
                }
                else
                {
                    splineMovement.Path = null;
                    splineMovement.ReleaseOnJump = false;
                    player.PlayerInput.TwodimensionalMode = false;
                    splineMovement.AggressiveDepthCorrection = false;
                }
            }
        }
        else
        {
            for (int i = 0; i < other.GetChildCount(); i++)
            {
                var npsm = other.GetChild(i).GetNodeOrNull<NonPlayerSplineMovement>(".");
                if (npsm != null && npsm.Path == this.Path)
                {
                    if (OnExitTrigger != null)
                    {
                        OnExitTrigger.OnBodyEnter(other);
                    }
                    else
                    {
                        npsm.Path = null;
                    }
                }
            }
        }
    }
}
