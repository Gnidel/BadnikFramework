using System;
using System.Threading.Tasks.Dataflow;
using Godot;

public partial class LoopingBossArena : Node3D
{
    // Player manipulation
    [Export]
    public Vector3 ConstantPush;

    // Arena manipulation
    [Export]
    public Node3D[] ArenaSegments = Array.Empty<Node3D>();
    private float movedDistance = 0;

    [Export]
    public float MaxMoveDistance;

    [Export]
    public Vector3 TeleportOffset;
    private int SegmentToTeleport = 0;

    public override void _Ready() { }

    public override void _PhysicsProcess(double delta)
    {
        if (StageData.Instance.IsFinished)
            return;

        // Arena manipulation
        movedDistance += (ConstantPush.Length() * (float)delta * 2);
        for (int i = 0; i < ArenaSegments.Length; i++)
        {
            var segment = ArenaSegments[i];
            if (movedDistance > MaxMoveDistance && i == SegmentToTeleport)
            {
                movedDistance = 0;
                segment.GlobalPosition +=
                    -ConstantPush.Normalized() * MaxMoveDistance * (ArenaSegments.Length);
                SegmentToTeleport = (SegmentToTeleport + 1) % ArenaSegments.Length;
            }
            segment.GlobalPosition += (ConstantPush * (float)delta * 2);
        }
    }
}
