using System;
using Godot;

public partial class TouchButtonsPositioning : Node
{
    [Export]
    public Node2D[] BottomLeft = new Node2D[0];

    [Export]
    public Node2D[] BottomRight = new Node2D[0];

    [Export]
    public Node2D[] TopLeft = new Node2D[0];

    [Export]
    public Node2D[] TopRight = new Node2D[0];

    [Export]
    public Node2D[] CharaSwapButtons = new Node2D[0];

    [Export]
    public Node2D SuperButton;

    public override void _Process(double delta)
    {
        // It's not in _Ready() because even on mobile screen can be resized at any point (for example when doing split screen)
        foreach (var element in BottomLeft)
        {
            element.GlobalPosition = new Vector2(
                GetViewport().GetVisibleRect().Position.X,
                GetViewport().GetVisibleRect().End.Y
            );
        }
        foreach (var element in BottomRight)
        {
            element.GlobalPosition = GetViewport().GetVisibleRect().End;
        }
        foreach (var element in TopLeft)
        {
            element.GlobalPosition = GetViewport().GetVisibleRect().Position;
        }
        foreach (var element in TopRight)
        {
            element.GlobalPosition = new Vector2(
                GetViewport().GetVisibleRect().End.X,
                GetViewport().GetVisibleRect().Position.Y
            );
        }

        bool charaSwapEnabled = (
            PlayersManager.Instance != null && PlayersManager.Instance.CharacterSwappingEnabled
        );
        foreach (var element in CharaSwapButtons)
        {
            element.Visible = charaSwapEnabled;
            element.ProcessMode = charaSwapEnabled
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }

        if (SuperButton != null)
        {
            foreach (var player in PlayerController.Instances)
            {
                if (player.NpcPartnerControl.IsNpc)
                    continue;
                var actionSuper = player.GetNodeOrNull<ActionSuperTransformation>(
                    "./PlayerControl/Actions/ActionSuperTransformation"
                );
                bool canSuper =
                    actionSuper != null
                    && (actionSuper.CheckInventoryRequirements() || actionSuper.IsSuper);
                SuperButton.Visible = canSuper;
                SuperButton.ProcessMode = canSuper
                    ? ProcessModeEnum.Inherit
                    : ProcessModeEnum.Disabled;
            }
        }
    }
}
