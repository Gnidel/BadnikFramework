using System;
using Godot;

public partial class Hitbox : Area3D
{
    [Export]
    public bool CanDamagePlayer = true;

    [Export]
    public bool CanDamageRollingPlayer = true;

    [Export]
    public bool CanDamageEnemy = true;

    [Export]
    public bool IsInstaKill = false;

    [Export]
    public PlayerController ResponsiblePlayer;

    public void NotifySuccessfulHit(Hurtbox hurtbox)
    {
        EmitSignal(SignalName.OnSuccessfulHit, this, hurtbox);
    }

    public void SetAllConditionsToFalse()
    {
        CanDamagePlayer = false;
        CanDamageRollingPlayer = false;
        CanDamageEnemy = false;
        IsInstaKill = false;
    }

    [Signal]
    public delegate void OnSuccessfulHitEventHandler(Hitbox hitbox, Hurtbox hurtbox);
}
