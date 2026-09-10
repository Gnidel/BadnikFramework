using System;
using Godot;

public partial class InfiniteItemSpawner : Node3D
{
    [Export]
    public PackedScene ItemTemplate;

    [Export]
    public int MinCount = 0;

    [Export]
    public int MaxCount = 1;

    [Export]
    public float SpawnRate = 1f;

    [Export]
    public float spawnTimer;

    [Export]
    public Vector3 InitialRelativeVelocity = Vector3.Up;

    [Export]
    public Node3D Container;

    public override void _PhysicsProcess(double delta)
    {
        if (Container.GetChildCount() >= MaxCount)
        {
            spawnTimer = 0;
            return;
        }
        spawnTimer += (float)delta;
        if (Container.GetChildCount() < MinCount)
        {
            spawnTimer += (float)delta;
        }
        if (spawnTimer < SpawnRate)
        {
            return;
        }
        var newItem = ItemTemplate.Instantiate<Node3D>();
        Container.AddChild(newItem);
        newItem.Position = Vector3.Zero;

        var rb = newItem.GetNodeOrNull<RigidBody3D>(".");
        if (rb != null)
        {
            rb.LinearVelocity = this.GlobalBasis.GetRotationQuaternion() * InitialRelativeVelocity;
        }
        spawnTimer = 0;
    }
}
