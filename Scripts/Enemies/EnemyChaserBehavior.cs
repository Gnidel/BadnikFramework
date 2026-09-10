using System;
using System.Linq;
using Godot;

public partial class EnemyChaserBehavior : Node
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public AnimationTree AnimationTree;

    [Export]
    public Laser[] Lasers = new Laser[0];

    public override void _Ready()
    {
        foreach (var laser in Lasers)
        {
            laser.SetLaserActive(false);
        }
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            AnimationTree.Get("parameters/playback");
    }

    public override void _ExitTree()
    {
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            AnimationTree.Get("parameters/playback");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (EnemyControl.HP == 0)
        {
            foreach (var laser in Lasers)
            {
                laser.QueueFree();
            }
            Lasers = new Laser[0];
        }
    }

    public void Fire()
    {
        if (EnemyControl.HP == 0)
        {
            return; // Dead enemies don't fire
        }
        foreach (var laser in Lasers)
        {
            laser.SetLaserActive(true);
        }

        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            AnimationTree.Get("parameters/playback");
        stateMachine.Travel("Fire");
    }

    public void StopFire()
    {
        foreach (var laser in Lasers)
        {
            laser.SetLaserActive(false);
        }
    }
}
