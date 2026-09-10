using System;
using Godot;

public partial class CluckerBehavior : Node
{
    [Export]
    public RigidBody3D EnemyRoot;

    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public AnimationPlayer AnimationPlayer;

    [Export]
    public AudioStreamPlayer3D GunAudioPlayer; // Assumption: gun fire clip is selected

    //[Export] public StuffSpawner BulletSpawner;
    [Export]
    public BulletHellShooter BulletHellShooter;

    [Export]
    public float FireInterval = 0.2f;
    private float fireTimer = 0f;

    [Export]
    public float MaxDistance = 100f;

    [Export]
    public float RotationSpeed = 3f;

    public override void _PhysicsProcess(double delta)
    {
        // Find the nearest player to target
        PlayerController Target = null;
        foreach (var player in PlayerController.Instances)
        {
            if (player.NpcPartnerControl.IsNpc)
                continue;
            var o = player.GlobalPosition - EnemyRoot.GlobalPosition;
            if (o.Length() > MaxDistance)
                continue;
            if (
                Target != null
                && (Target.GlobalPosition - EnemyRoot.GlobalPosition).Length() < o.Length()
            )
                continue;
            Target = player;
        }
        if (Target == null)
            return;

        // Rotate towards player
        var offset = Target.GlobalPosition - EnemyRoot.GlobalPosition;
        var flatOffset = offset.ProjectOnPlane(EnemyRoot.GlobalBasis.Y);
        EnemyRoot.LookAt(
            EnemyRoot.GlobalPosition
                + (-EnemyRoot.GlobalBasis.Z).Lerp(
                    flatOffset.Normalized(),
                    (float)delta * RotationSpeed
                ),
            EnemyRoot.GlobalBasis.Y
        );

        // FIRE!
        fireTimer += (float)delta;
        if (fireTimer > FireInterval)
        {
            fireTimer = 0f;
            //BulletSpawner.Spawn();

            float rayDistance = 100f;
            var rayQuery = PhysicsRayQueryParameters3D.Create(
                BulletHellShooter.GlobalPosition,
                BulletHellShooter.GlobalPosition + -EnemyRoot.GlobalBasis.Z * rayDistance,
                1 /*default*/
            );

            var rayResult = EnemyRoot.GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);

            if (rayResult.Count > 0)
            {
                rayDistance = (
                    rayResult["position"].AsVector3() - BulletHellShooter.GlobalPosition
                ).Length();
            }
            BulletHellShooter.Shoot(
                BulletHellShooter.GlobalPosition,
                -EnemyRoot.GlobalBasis.Z * 20,
                rayDistance
            );

            GunAudioPlayer.Play(0);
        }
    }
}
