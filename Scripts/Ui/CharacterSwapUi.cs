using System;
using Godot;

public partial class CharacterSwapUi : ColorRect
{
    [Export]
    public OnePlayerUI OnePlayerUI;

    [Export]
    public string PrevCharacterButtonLabel = "[input]actionjump[/input]";

    [Export]
    public string NextCharacterButtonLabel = "[input]actionjump[/input]";

    [Export]
    public Label CurrentCharacterLabel;

    [Export]
    public ButtonPromptTextLabel PrevButtonLabel;

    [Export]
    public ButtonPromptTextLabel NextButtonLabel;

    public override void _Process(double delta)
    {
        if (PlayersManager.Instance == null)
        {
            return;
        }

        if (
            PlayersManager.Instance.CharacterSwappingEnabled
            && PlayerController.Instances.Count > 1
        )
        {
            var playerId = OnePlayerUI.PlayerID;
            CurrentCharacterLabel.Text = PlayerController.Instances[playerId].CharacterName;
            PrevButtonLabel.Text =
                PrevCharacterButtonLabel
                + " "
                + PlayersManager.Instance.GetNextCharacterName(playerId, -1);
            PrevButtonLabel.HandleInputTag();

            NextButtonLabel.Text =
                NextCharacterButtonLabel
                + " "
                + PlayersManager.Instance.GetNextCharacterName(playerId, +1);
            NextButtonLabel.HandleInputTag();
            this.Visible = true;
        }
        else
        {
            this.Visible = false;
        }
    }
}
