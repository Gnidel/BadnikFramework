using System;
using System.Collections.Generic;
using Godot;
using Godot.NativeInterop;

public partial class BulletHellShooter : Node3D
{
    private class BulletInstance
    {
        public Vector3 GlobalPosition;
        public Vector3 LinearVelocity;
        public float Lifetime = 0;
        public float RemainingRange;
    }

    private List<BulletInstance> Bullets = new List<BulletInstance>();

    [Export]
    public MultiMeshInstance3D MultiMeshInstance3D;

    [Export]
    public float MaxLifetime = 10f;

    [Export]
    public float MaxDistance = 100f;

    [Export]
    public float DamageRadius = 0.5f;

    [Export]
    public Hitbox HitboxData;

    public void Shoot(Vector3 GlobalPosition, Vector3 LinearVelocity, float remainingRange)
    {
        Bullets.Add(
            new BulletInstance
            {
                GlobalPosition = GlobalPosition,
                LinearVelocity = LinearVelocity,
                RemainingRange = remainingRange,
            }
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if (MultiMeshInstance3D == null)
        {
            GD.PrintErr("No MultiMesh on shooter {GetInstanceId()}");
        }
        this.GlobalBasis = Basis.Identity;
        // Bullets control
        List<BulletInstance> bulletsToRemove = new List<BulletInstance>();
        foreach (var bullet in Bullets)
        {
            bullet.GlobalPosition += bullet.LinearVelocity * (float)delta;
            if ((bullet.GlobalPosition - this.GlobalPosition).Length() > MaxDistance)
            {
                bulletsToRemove.Add(bullet);
                continue;
            }

            bullet.Lifetime += (float)delta;

            if (bullet.Lifetime > MaxLifetime)
            {
                bulletsToRemove.Add(bullet);
                continue;
            }

            bullet.RemainingRange -= bullet.LinearVelocity.Length() * (float)delta;
            if (bullet.RemainingRange <= 0)
            {
                bulletsToRemove.Add(bullet);
                continue;
            }
        }

        foreach (var bulletToRemove in bulletsToRemove)
        {
            Bullets.Remove(bulletToRemove);
        }

        // Visual
        MultiMeshInstance3D.Multimesh.InstanceCount = Bullets.Count;
        for (int i = 0; i < Bullets.Count; i++)
        {
            Vector3 localPos = this.GlobalTransform.AffineInverse() * Bullets[i].GlobalPosition;
            Transform3D t = new Transform3D(Basis.Identity, localPos);
            MultiMeshInstance3D.Multimesh.SetInstanceTransform(i, t);
        }

        // Damage calculation
        foreach (var bullet in Bullets)
        {
            foreach (var player in PlayerController.Instances)
            {
                var playerChestPosition =
                    player.GlobalPosition + player.GlobalBasis.Y.Normalized() * 0.5f;
                if ((playerChestPosition - bullet.GlobalPosition).Length() < DamageRadius)
                {
                    player.PlayerDamage.OnHitboxEnter(HitboxData, null);
                }
            }
        }
    }
}
