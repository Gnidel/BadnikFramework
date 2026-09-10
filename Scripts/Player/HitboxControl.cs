using System;
using Godot;

public partial class HitboxControl : Node
{
    [Export]
    public Hitbox Hitbox;

    [Export]
    public Node[] /*IActionUsingHitbox[]*/
    ActionsUsingHitbox = new Node[0];

    public override void _PhysicsProcess(double delta)
    {
        foreach (var a in ActionsUsingHitbox)
        {
            var auh = a.GetNode<IActionUsingHitbox>(".");
            if (auh.IsHitboxEnabled())
            {
                Hitbox.ProcessMode = ProcessModeEnum.Inherit;
                return;
            }
        }
        Hitbox.ProcessMode = ProcessModeEnum.Disabled;
    }
}
