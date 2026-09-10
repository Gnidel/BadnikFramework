using System;
using Godot;

public partial class ChasePlayerBehavior : Node
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public float Speed;

    [Export]
    public float Acceleration = 10f;

    [Export]
    public float StopAtXZDistance = 10f;

    [Export]
    public Node3D ChaseTarget;

    [Export]
    public bool LimitToXZ;

    [Export]
    public bool LimitToYZ;

    [Export]
    public RigidBody3D Rigidbody;

    public override void _PhysicsProcess(double delta)
    {
        if (ChaseTarget != null)
        {
            var dir = (ChaseTarget.GlobalPosition - Rigidbody.GlobalPosition);
            if (dir.ProjectOnPlane(EnemyControl.Gravity).Length() < StopAtXZDistance)
            {
                dir = Vector3.Zero;
            }
            if (LimitToXZ)
            {
                dir = dir.ProjectOnPlane(EnemyControl.Gravity);
            }
            if (LimitToYZ)
            {
                dir = dir.ProjectOnPlane(Rigidbody.Basis.X);
            }

            var velocityXZ = Rigidbody.LinearVelocity.ProjectOnPlane(EnemyControl.Gravity);
            var velocityY = Rigidbody.LinearVelocity.Project(EnemyControl.Gravity);

            Rigidbody.LinearVelocity =
                velocityXZ.Lerp(dir.Normalized() * Speed, (float)delta * Acceleration) + velocityY;
        }
        else
        {
            FindNearestTarget();
        }
    }

    void FindNearestTarget()
    {
        Node3D nearestPlayer = null;
        foreach (var player in PlayerController.Instances)
        {
            if (nearestPlayer == null)
            {
                nearestPlayer = player;
                continue;
            }
            if (
                Rigidbody.GlobalPosition.DistanceTo(nearestPlayer.GlobalPosition)
                < Rigidbody.GlobalPosition.DistanceTo(player.GlobalPosition)
            )
            {
                nearestPlayer = player;
            }
        }

        ChaseTarget = nearestPlayer;
    }
}
