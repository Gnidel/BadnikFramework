using System;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class GrindRail : Path3D
{
    [Export]
    public Area3D Hitbox;

    [Export]
    public CollisionShape3D BoundingBox;

    [Export]
    public CsgPolygon3D RailHitboxMesh;

    [Export]
    public CsgPolygon3D RailVisibleMesh;

    [Export]
    public bool Loop;

    public override void _Ready()
    {
        BoundingBox.Shape = new ConcavePolygonShape3D();

        OnCurveChanged();
        RailHitboxMesh.ProcessMode = ProcessModeEnum.Disabled; // CsgPolygon3D is easy to use, but performance drain.

        if (RailVisibleMesh != null)
        {
            RailVisibleMesh.ProcessMode = ProcessModeEnum.Disabled;
        }

        if (Hitbox != null)
        {
            Hitbox.BodyEntered += this.OnBodyEntered;
        }
    }

    public void OnCurveChanged()
    {
        ApplyTrimeshShapeAfterFrame();
    }

    async void ApplyTrimeshShapeAfterFrame()
    {
        RailHitboxMesh.ProcessMode = ProcessModeEnum.Inherit;
        // Sometimes mesh isn't built yet. It mostly happens when the curve has been changed at runtime. Wait a frame and then apply.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        //if (RailHitboxMesh.GetMeshes().Count > 0)
        {
            BoundingBox.Shape = RailHitboxMesh.GetMeshes()[1].As<Mesh>().CreateTrimeshShape();
        }
        RailHitboxMesh.ProcessMode = ProcessModeEnum.Disabled;
    }

    public void OnBodyEntered(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            if (player.PlayerDamage.IsDead)
                return;

            var actionGrind = other.GetNodeOrNull<ActionGrind>(
                "./PlayerControl/Actions/ActionGrind"
            );
            if (actionGrind != null && actionGrind.CanProcess())
            {
                if (!actionGrind.CanGrind())
                    return;
                actionGrind.railSwapWeight = 1;
                StartGrind(player, actionGrind);
            }
        }
    }

    public void StartGrind(PlayerController player, ActionGrind actionGrind)
    {
        var progress = this.Curve.GetClosestOffset(player.GlobalPosition - this.GlobalPosition);
        var sample = this.GlobalTransform * this.Curve.SampleBakedWithRotation(progress);

        var follower = new PathFollow3D();
        follower.Loop = this.Loop;
        this.AddChild(follower);

        follower.Progress = progress;
        follower.GlobalTransform = sample;
        follower.ResetPhysicsInterpolation();

        actionGrind.StartGrind(follower, sample);
    }
}
