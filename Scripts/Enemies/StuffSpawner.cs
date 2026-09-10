using System;
using Godot;

public partial class StuffSpawner : Node3D
{
    //[Export] public RigidBody3D Template;
    [Export]
    public PackedScene Template;

    [Export]
    public Vector3 InitialRelativeVelocity = -Vector3.Up * 5;

    public override void _Ready()
    {
        //Template.Visible = false;
        //Template.ProcessMode = ProcessModeEnum.Disabled;
        //Template.GlobalPosition = this.GlobalPosition;
    }

    public void Spawn()
    {
        //var spawnedItem = Template.Duplicate((int)DuplicateFlags.UseInstantiation).GetNode<RigidBody3D>(".");
        var spawnedItem = Template.Instantiate<RigidBody3D>();
        this.AddChild(spawnedItem);
        spawnedItem.Visible = true;
        spawnedItem.ProcessMode = ProcessModeEnum.Inherit;
        spawnedItem.GlobalPosition = this.GlobalPosition;
        spawnedItem.LinearVelocity =
            this.GlobalBasis.GetRotationQuaternion() * InitialRelativeVelocity;
    }
}
