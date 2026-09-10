using System;
using Godot;

[Tool]
public partial class CollissionShapeFromCSGPolygon : Node
{
    [Export]
    public CollisionShape3D Shape;

    [Export]
    public CsgPolygon3D CsgMesh;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            OnCurveChanged();
        }
    }

    public void OnCurveChanged()
    {
        //var mesh = CsgMesh.GetMeshes()[1].As<Mesh>().CreateTrimeshShape();
        //Shape.Shape = mesh;

        ApplyTrimeshShapeAfterFrame();
    }

    async void ApplyTrimeshShapeAfterFrame()
    {
        CsgMesh.ProcessMode = ProcessModeEnum.Inherit;
        // Sometimes mesh isn't built yet. It mostly happens when the curve has been changed at runtime. Wait a frame and then apply.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        //if (RailHitboxMesh.GetMeshes().Count > 0)
        {
            Shape.Shape = CsgMesh.GetMeshes()[1].As<Mesh>().CreateTrimeshShape();
        }
        CsgMesh.ProcessMode = ProcessModeEnum.Disabled;
    }
}
