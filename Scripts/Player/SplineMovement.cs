using System;
using Godot;

public partial class SplineMovement : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionGrind ActionGrind;

    [Export]
    public ActionHoming ActionHoming;

    [Export]
    public bool ReleaseOnJump;
    public bool WasGrounded = false; // Used for ReleaseOnJump

    [Export]
    public bool ReleaseOnEnds = false;

    [Export]
    public float ReleaseOnDistanceFromPath; // Values <=0 mean that it's unused

    [Export]
    public Path3D Path;

    [Export]
    public bool SpeedLimit = false;

    [Export]
    public float MinSpeedLimit = 50f;

    [Export]
    public float MaxSpeedLimit = 50f;

    [Export]
    public bool AggressiveDepthCorrection = false; // Basically teleport. It's good for 2D loops or very tight places.

    public Spline2DTrigger Trigger;

    public override void _Ready()
    {
        if (ActionGrind == null)
        {
            ActionGrind = this.GetNodeOrNull<ActionGrind>("../Actions/ActionGrind");
        }
        if (ActionHoming == null)
        {
            ActionHoming = this.GetNodeOrNull<ActionHoming>("../Actions/ActionHoming");
        }
    }

    public void CopyStateFrom(SplineMovement source)
    {
        Path = source.Path;
        WasGrounded = source.WasGrounded;
        ReleaseOnJump = source.ReleaseOnJump;
        ReleaseOnEnds = source.ReleaseOnEnds;
        ReleaseOnDistanceFromPath = source.ReleaseOnDistanceFromPath;
        SpeedLimit = source.SpeedLimit;
        MinSpeedLimit = source.MinSpeedLimit;
        MaxSpeedLimit = source.MaxSpeedLimit;
        AggressiveDepthCorrection = source.AggressiveDepthCorrection;
        Trigger = source.Trigger;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (
            Path == null
            || !GodotObject.IsInstanceValid(Player)
            || !Player.IsInsideTree()
            || Player.GetWorld3D() == null
        )
            return;
        if (ReleaseOnJump && Player.Grounded)
        {
            WasGrounded = true;
        }
        if (ReleaseOnJump && !Player.Grounded && WasGrounded)
        {
            Path = null;
            Player.PlayerInput.TwodimensionalMode = false;

            if (Trigger != null && Trigger.OnExitTrigger != null)
            {
                Trigger.OnExitTrigger.OnBodyEnter(Player);
            }
            else
            {
                Trigger = null;
            }

            return;
        }

        if (ActionGrind.IsGrinding() || ActionHoming.IsHoming)
            return;
        if (Player.LockedVelocity)
            return;

        var nearestOffset = Path.Curve.GetClosestOffset(
            Player.GlobalPosition - Path.GlobalPosition
        );

        if (ReleaseOnEnds && (nearestOffset <= 0 || nearestOffset >= 1))
        {
            Path = null;
            Player.PlayerInput.TwodimensionalMode = false;

            if (Trigger != null && Trigger.OnExitTrigger != null)
            {
                Trigger.OnExitTrigger.OnBodyEnter(Player);
            }
            else
            {
                Trigger = null;
            }
            return;
        }

        Transform3D sample = Path.Curve.SampleBakedWithRotation(nearestOffset, false, true);

        if (ReleaseOnDistanceFromPath > 0)
        {
            var distance = Path.ToGlobal(sample.Origin).DistanceTo(Player.GlobalPosition);

            if (distance > ReleaseOnDistanceFromPath)
            {
                Path = null;
                Player.PlayerInput.TwodimensionalMode = false;

                if (AggressiveDepthCorrection)
                {
                    //Player.GroundedLocked = false;
                    //Player.Enabled = true;
                }
                if (Trigger != null && Trigger.OnExitTrigger != null)
                {
                    Trigger.OnExitTrigger.OnBodyEnter(Player);
                }
                else
                {
                    Trigger = null;
                }

                return;
            }
        }

        var offset = (sample.Origin + Path.GlobalPosition) - Player.GlobalPosition;
        var depthOffset = Vector3Utils.Project(offset, sample.Basis.X);

        var originalVelocity = Player.VelocityXZ.Length();
        //var originalVelocity = Player.LinearVelocity.Length();
        if (SpeedLimit)
        {
            originalVelocity = Mathf.Clamp(originalVelocity, MinSpeedLimit, MaxSpeedLimit);
            Player.PlayerInput.LeftInputTimedLock = Mathf.Max(
                Player.PlayerInput.LeftInputTimedLock,
                0.1f
            );
        }
        Player.VelocityXZ = Vector3Utils.ProjectOnPlane(
            Player.VelocityXZ,
            sample.Basis.X.Normalized()
        );
        if (!AggressiveDepthCorrection)
        {
            Player.VelocityXZ += depthOffset * originalVelocity / 10f;
        }
        Player.VelocityXZ =
            Vector3Utils.ProjectOnPlane(Player.VelocityXZ, Player.GroundNormal).Normalized()
            * originalVelocity;

        if (AggressiveDepthCorrection)
        {
            //Player.Grounded = true;
            //Player.GroundNormal = sample.Basis.Y;
            //Player.GroundedLocked = true;
            //Player.Enabled = false;
            //Player.GlobalBasis = sample.Basis;

            //var xyOffset = Vector3Utils.ProjectOnPlane(offset, sample.Basis.Z);
            if (depthOffset.Length() > 0.1f)
            {
                if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
                {
                    var cam = PlayerCamera.Instances[Player.PlayerID];
                    cam.StartLerp(0.25f);
                }
            }
            Player.MoveAndCollide(depthOffset);
        }

        //Player.LinearVelocity = Vector3Utils.ProjectOnPlane(Player.LinearVelocity, sample.Basis.X.Normalized()).Normalized() * originalVelocity;
        //Player.LinearVelocity += depthOffset * originalVelocity / 10f;

        //Player.VelocityXZ = Vector3Utils.ProjectOnPlane(Player.VelocityXZ, Player.GroundNormal).Normalized() * originalVelocity;

        Player.PlayerInput.Plane2DControlsX = -sample.Basis.Z;

        if (Player.PlayerInput.LockedInput)
        {
            Player.PlayerInput.LeftInput3D =
                Player.PlayerInput.LeftInput3D.ProjectOnPlane(sample.Basis.X).Normalized()
                * Player.PlayerInput.LeftInput3D.Length();
        }
    }
}
