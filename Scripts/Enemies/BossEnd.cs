using System;
using Godot;

public partial class BossEnd : Node
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public float TimeToDestroy = 5;
    private float destroyTimer = 0;

    public override void _PhysicsProcess(double delta)
    {
        if (destroyTimer == 0)
        {
            foreach (var cam in PlayerCamera.Instances.Values)
            {
                cam.CameraTargets.Clear();
                cam.CameraTargets.Add(cam.Player);
                cam.Mode = PlayerCamera.CameraMode.Free;
            }
        }
        if (EnemyControl.DeathAnticipationParticle != null)
        {
            EnemyControl.DeathAnticipationParticle.Emitting = true;
        }
        destroyTimer += (float)delta;
        if (destroyTimer > TimeToDestroy && EnemyControl.RigidBody != null)
        {
            EnemyControl.RigidBody.QueueFree();
        }
    }
}
