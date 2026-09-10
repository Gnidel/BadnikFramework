using System;
using System.Collections.Generic;
using Godot;

public partial class MapUIIconTrackedObject : Node3D
{
    public static List<MapUIIconTrackedObject> Instances = new List<MapUIIconTrackedObject>();

    [Export]
    public Texture2D Texture;

    [Export]
    public Node3D FastTravelPoint; // Set to null to disable fast travel

    [Export(PropertyHint.MultilineText)]
    public string Description;

    public override void _EnterTree()
    {
        Instances.Add(this);
        if (MapUIControl.Instance != null)
        {
            MapUIControl.Instance.RefreshIcons();
        }
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
    }
}
