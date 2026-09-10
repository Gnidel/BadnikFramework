using System;
using Godot;

public partial class HideOnMobile : Node
{
    [Export]
    bool ShowOnMobile = true;

    [Export]
    bool ShowOnNonMobile = false;

    [Export]
    bool ApplyToParent = false;

    [Export]
    bool Destroy = false;

    public override void _Ready()
    {
        var platform = OS.GetName();
        var path = ApplyToParent ? ".." : ".";
        Node node = this.GetNodeOrNull<Node>(path);
        Node2D node2D = this.GetNodeOrNull<Node2D>(path);
        Control control = this.GetNodeOrNull<Control>(path);
        Node3D node3D = this.GetNodeOrNull<Node3D>(path);
        if (platform == "Android" || platform == "iOS")
        {
            if (node2D != null)
                node2D.Visible = ShowOnMobile;
            if (node3D != null)
                node3D.Visible = ShowOnMobile;
            if (control != null)
                node3D.Visible = ShowOnMobile;
            node.ProcessMode = ShowOnMobile ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            if (Destroy && !ShowOnMobile)
            {
                node.QueueFree();
            }
        }
        else
        {
            if (node2D != null)
                node2D.Visible = ShowOnNonMobile;
            if (node3D != null)
                node3D.Visible = ShowOnNonMobile;
            if (control != null)
                node3D.Visible = ShowOnNonMobile;
            node.ProcessMode = ShowOnNonMobile ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            if (Destroy && !ShowOnNonMobile)
            {
                node.QueueFree();
            }
        }
    }
}
