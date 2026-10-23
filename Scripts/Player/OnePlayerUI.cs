using System;
using System.Collections.Generic;
using Godot;

public partial class OnePlayerUI : Control
{
    [Export]
    public int PlayerID;

    [Export]
    public Node2D HomingIcon;

    [Export]
    public Node2D HomingOffscreenIcon;

    [Export]
    public Control BoostUIContainer;

    [Export]
    public ProgressBar BoostMeter;

    public PlayerInput.InputType LastInputType = PlayerInput.InputType.NONE;

    public static Dictionary<int, OnePlayerUI> Instances = new Dictionary<int, OnePlayerUI>();

    private ActionBoost ActionBoost;
    private PlayerInput activePlayerInput;

    public override void _EnterTree()
    {
        Instances.Add(PlayerID, this);
    }

    public override void _ExitTree()
    {
        Instances.Remove(PlayerID);
    }

    public override void _Process(double delta)
    {
        HandleBoostBar();
    }

    public override void _Ready()
    {
        OnCharacterSwap();
    }

    public void OnCharacterSwap()
    {
        var newActivePlayerInput = PlayerController.Instances[PlayerID].PlayerInput;
        if (activePlayerInput != null)
        {
            activePlayerInput.OnInputTypeChange -= OnActiveInputTypeChange;
        }

        if (LastInputType == PlayerInput.InputType.NONE)
        {
            LastInputType = newActivePlayerInput.LastInputType;
        }
        else
        {
            newActivePlayerInput.LastInputType = LastInputType;
        }

        activePlayerInput = newActivePlayerInput;
        activePlayerInput.OnInputTypeChange += OnActiveInputTypeChange;

        ActionBoost = PlayerController
            .Instances[PlayerID]
            .GetNodeOrNull<ActionBoost>("./PlayerControl/Actions/ActionBoost");
        HomingIcon.Visible = false;
    }

    private void OnActiveInputTypeChange()
    {
        LastInputType = activePlayerInput.LastInputType;
    }

    void HandleBoostBar()
    {
        bool showBoostBar =
            ActionBoost != null
            && ActionBoost.ProcessMode != ProcessModeEnum.Disabled
            && !ActionBoost.RequireSuperTransformation
            && !StageData.Instance.IsFinished
            && ActionBoost.ShowBoostMeter;
        BoostUIContainer.Visible = showBoostBar;
        if (!showBoostBar)
        {
            return;
        }
        if (ActionBoost != null)
        {
            BoostMeter.Value = ActionBoost.Fuel * 100;
        }
    }
}
