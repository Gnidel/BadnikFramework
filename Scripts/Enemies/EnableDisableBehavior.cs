using System;
using Godot;

public partial class EnableDisableBehavior : Node
{
    [Export]
    public Node[] NodesToEnable = new Node[0];

    [Export]
    public Node[] NodesToDisable = new Node[0];

    public override void _Process(double delta)
    {
        // TODO: Calling it every frame feels suboptimal
        foreach (var node in NodesToEnable)
        {
            node.ProcessMode = ProcessModeEnum.Inherit;
            var canvasItem = node.GetNodeOrNull<CanvasItem>(".");
            if (canvasItem != null)
            {
                canvasItem.Visible = true;
            }
            var node3D = node.GetNodeOrNull<Node3D>(".");
            if (node3D != null)
            {
                node3D.Visible = true;
            }
        }
        foreach (var node in NodesToDisable)
        {
            node.ProcessMode = ProcessModeEnum.Disabled;
            var canvasItem = node.GetNodeOrNull<CanvasItem>(".");
            if (canvasItem != null)
            {
                canvasItem.Visible = false;
            }
            var node3D = node.GetNodeOrNull<Node3D>(".");
            if (node3D != null)
            {
                node3D.Visible = false;
            }
        }
    }
}
