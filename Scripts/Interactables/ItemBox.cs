using System;
using System.Linq;
using Godot;

public partial class ItemBox : Node
{
    [Export]
    public Node3D Box;

    [Export]
    public Node3D ThingInside;

    [Export]
    public Vector3 RotationAxisOfThingInside;

    [Export]
    public float RotationSpeedDegrees;

    [Export]
    public Node3D SpawnCenter;

    [Export]
    public PackedScene ItemToRelease;

    [Export]
    public int ItemsCount = 1;

    [Export]
    public float SpawnRadius = 5f;

    [Export]
    public PackedScene ExplosionParticles;

    [Export]
    public bool LookAtPlayer = true;

    public override void _Ready()
    {
        if (
            ItemToRelease != null
            && ResourceLoader.LoadThreadedGetStatus(ItemToRelease.ResourcePath)
                == ResourceLoader.ThreadLoadStatus.InvalidResource
        )
        {
            ResourceLoader.LoadThreadedRequest(ItemToRelease.ResourcePath);
        }

        if (
            ExplosionParticles != null
            && ResourceLoader.LoadThreadedGetStatus(ExplosionParticles.ResourcePath)
                == ResourceLoader.ThreadLoadStatus.InvalidResource
        )
        {
            ResourceLoader.LoadThreadedRequest(ExplosionParticles.ResourcePath);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        ThingInside.Rotate(
            RotationAxisOfThingInside.Normalized(),
            Mathf.DegToRad(RotationSpeedDegrees) * (float)delta
        );
        if (LookAtPlayer)
        {

            PlayerController nearestPlayer = PlayerController.Instances[0];
            if (PlayerController.Instances.Count > 1)
            {
                foreach (var player in PlayerController.Instances)
                {
                    if (
                        player.GlobalPosition.DistanceTo(Box.GlobalPosition)
                        < nearestPlayer.GlobalPosition.DistanceTo(Box.GlobalPosition)
                    )
                    {
                        nearestPlayer = player;
                    }
                }
            }
            var nearestPlayerOffset = Box.GlobalPosition - nearestPlayer.GlobalPosition;
            var lookOffset = nearestPlayerOffset.ProjectOnPlane(Box.Basis.Y);
            if (lookOffset.Length() > 1)
            {
                Box.LookAt(Box.GlobalPosition + lookOffset, Box.GlobalBasis.Y);
            }
        }
    }

    public void OnHitboxEnter(Hitbox hitbox, Hurtbox hurtbox)
    {
        if (hitbox.CanDamageEnemy)
        {
            hitbox.NotifySuccessfulHit(hurtbox);
            ReleaseItems(hitbox);
            if (ExplosionParticles != null)
            {
                //var explosion = ExplosionParticles.Instantiate<Node3D>();
                ResourceLoader.LoadThreadedRequest(ExplosionParticles.ResourcePath);
                var explosion = (
                    ResourceLoader.LoadThreadedGet(ExplosionParticles.ResourcePath) as PackedScene
                ).Instantiate<Node3D>();

                this.GetParent().GetParent().AddChild(explosion);
                explosion.GlobalPosition = Box.GlobalPosition;

                var mep = explosion.GetNodeOrNull<MultiParticlePlayer>(".");
                if (mep != null)
                {
                    mep.Play();
                }
            }
            this.GetParent().QueueFree();
        }
    }

    void ReleaseItems(Hitbox hitbox)
    {
        ResourceLoader.LoadThreadedRequest(ItemToRelease.ResourcePath);
        PackedScene newItemScene =
            ResourceLoader.LoadThreadedGet(ItemToRelease.ResourcePath) as PackedScene;
        for (int i = 0; i < ItemsCount; i++)
        {
            var spawnPosition =
                SpawnCenter.GlobalPosition
                + SpawnCenter.GlobalBasis.GetRotationQuaternion()
                    * new Vector3(
                        Mathf.Sin((float)i / ItemsCount * 2 * Mathf.Pi),
                        0,
                        Mathf.Cos((float)i / ItemsCount * 2 * Mathf.Pi)
                    );

            var newItem = newItemScene.Instantiate<Node3D>();
            this.GetNode("../..").AddChild(newItem);
            newItem.GlobalPosition = spawnPosition;

            var collectableItem = newItem.GetNodeOrNull<CollectableItem>(".");
            if (collectableItem != null)
            {
                if (hitbox.ResponsiblePlayer != null)
                {
                    collectableItem.AttractiveNode = hitbox.ResponsiblePlayer;
                }
                else
                {
                    collectableItem.AttractiveNode = hitbox;
                }
                collectableItem.AttractionTimer -= (float)i / 10f;
            }
        }
    }
}
