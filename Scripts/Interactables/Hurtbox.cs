using System;
using Godot;

public partial class Hurtbox : Area3D
{
    public enum BounceStyleEnum
    {
        GoForward,
        BounceBack,
        ConstantDir,
    }

    [Export]
    public BounceStyleEnum BounceStyle;

    [Export]
    public Vector3 ConstantDir = -Vector3.Forward;

    public void OnAreaEnter(Area3D other)
    {
        var hitbox = other.GetNodeOrNull<Hitbox>(".");
        if (hitbox != null)
        {
            EmitSignal(SignalName.OnHitboxEnter, hitbox, this);
        }
    }

    public override void _Ready()
    {
        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.AreaEntered += this.OnAreaEnter;
        }
    }

    [Signal]
    public delegate void OnHitboxEnterEventHandler(Hitbox hitbox, Hurtbox hurtbox);
}
