using System;
using Godot;

public partial class DeathEggWeakSpot : Node3D
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public PackedScene Explosion;

    [Export]
    public float ExplosionTime = 2f;

    bool alreadyDestroyed = false;

    void OnDamage(Hitbox hitbox, Hurtbox hurtbox)
    {
        if (alreadyDestroyed)
        {
            return;
        }
        EnemyControl.HP -= 1;

        if (EnemyControl.HP <= 0)
        {
            EnemyControl.StartDying(hitbox, hurtbox);
        }

        EnemyControl.HealthBar.SetHP(EnemyControl.HP);
        alreadyDestroyed = true;
        var explosion = Explosion.Instantiate<MultiParticlePlayer>();
        this.GetParent().AddChild(explosion);
        explosion.GlobalPosition = this.GlobalPosition;

        explosion.Play();
        this.QueueFree();
    }
}
