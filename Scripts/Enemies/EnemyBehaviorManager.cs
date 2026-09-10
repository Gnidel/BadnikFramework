using System;
using Godot;

public partial class EnemyBehaviorManager : Node
{
    public EnemyControl EnemyControl; // Set by EnemyControl

    [Export]
    public Node CurrentPhase;

    public override void _Ready()
    {
        this.SetPhase(CurrentPhase);
    }

    public void SetPhase(Node NextPhase)
    {
        CurrentPhase = NextPhase;
        foreach (var phase in this.GetChildren())
        {
            if (phase == CurrentPhase)
            {
                phase.ProcessMode = ProcessModeEnum.Inherit;
            }
            else
            {
                phase.ProcessMode = ProcessModeEnum.Disabled;
            }
            phase.SetProcess(phase == CurrentPhase);
            phase.SetPhysicsProcess(phase == CurrentPhase);
        }
    }
}
