using System;
using Godot;

public partial class PlayerStatusUI : Control
{
    [Export]
    public int PlayerID;

    [Export]
    public RichTextLabel UiText;

    [Export]
    public Color NoRingsColor;

    [Export]
    public TextureRect[] RedRingsIcons = new TextureRect[0];

    [Export]
    public string[] RedRingsIdentifiers = new string[0];

    [Export]
    public Texture2D RedRingYesIcon;

    [Export]
    public Texture2D RedRingNoIcon;

    [Export]
    public Control ItemInfoContainer;

    [Export]
    public PackedScene ExtraItemTracker;

    public override void _Ready()
    {
        var fpsDisplay = GetNodeOrNull<Control>("FpsText");
        if (fpsDisplay != null)
        {
            fpsDisplay.Visible = OptionsMenu.Options.ShowFps;
            fpsDisplay.ProcessMode = OptionsMenu.Options.ShowFps
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }

        var stageData = StageData.Instance;
        if (stageData == null)
            return;

        foreach (var trackedItem in stageData.ExtraTrackedItems)
        {
            var tracker = ExtraItemTracker.Instantiate<ExtraItemTracker>();
            ItemInfoContainer.AddChild(tracker);
            tracker.PlayerID = PlayerID;
            tracker.TrackedItem = trackedItem.Key;
            tracker.Icon.Texture = trackedItem.Value;
        }
    }

    public override void _Process(double delta)
    {
        var player = GetPlayer();
        if (player == null || StageData.Instance == null)
            return;

        string timeText =
            "[b]Time: [/b]" + TimeSpan.FromSeconds(PlayerUI.CurrentTime).ToString(@"mm\:ss\:ff");
        int ringsCount = player.PlayerInventory.GetItemCount("rings");
        string ringsText = "[b]Rings: [/b]" + ringsCount;
        if (ringsCount == 0)
        {
            var colorString =
                ((int)NoRingsColor.R * 255).ToString("X2")
                + ((int)NoRingsColor.G * 255).ToString("X2")
                + ((int)NoRingsColor.B * 255).ToString("X2");
            ringsText = "[b]Rings: [/b][color=" + colorString + "]0[/color]";
        }

        string bonusText =
            "[b]Bonus: [/b]" + player.PlayerInventory.GetItemCount("bluespherebonus") + "x";

        UiText.Text = "";
        if (StageData.Instance.Style != StageData.StageStyle.HubStage)
            UiText.Text += timeText + "\n";
        if (StageData.Instance.Style != StageData.StageStyle.SpecialStage)
            UiText.Text += ringsText;
        else
            UiText.Text += "\n" + bonusText;

        UpdateRedRingsIcons(player);
    }

    private PlayerController GetPlayer()
    {
        if (PlayerID < 0 || PlayerID >= PlayerController.Instances.Count)
            return null;
        return PlayerController.Instances[PlayerID];
    }

    private void UpdateRedRingsIcons(PlayerController player)
    {
        for (int i = 0; i < RedRingsIcons.Length && i < RedRingsIdentifiers.Length; i++)
        {
            if (!StageData.Instance.HasRedRings)
            {
                RedRingsIcons[i].Visible = false;
                continue;
            }

            RedRingsIcons[i].Visible = true;
            var targetIcon = RedRingNoIcon;
            if (player.PlayerInventory.GetItemCount(RedRingsIdentifiers[i]) > 0)
                targetIcon = RedRingYesIcon;
            if (RedRingsIcons[i].Texture != targetIcon)
                RedRingsIcons[i].Texture = targetIcon;
        }
    }
}
