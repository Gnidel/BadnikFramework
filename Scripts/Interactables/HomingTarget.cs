using System;
using System.Collections.Generic;
using Godot;

public partial class HomingTarget : Node3D
{
    public static List<HomingTarget> Instances = new List<HomingTarget>();

    [Export]
    public bool TargetableByHomingAttack = true;

    [Export]
    public bool TargetableByGuns = true;

    public override void _EnterTree()
    {
        Instances.Add(this);
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
    }
}
