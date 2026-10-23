using System;
using System.Collections.Generic;
using Godot;

public partial class CharacterSwapUi : ColorRect
{
    [Export]
    public float SelectedBannerWidth = 330;

    [Export]
    public float SelectedBannerHeight = 120;

    [Export]
    public float UnselectedBannerWidth = 260;

    [Export]
    public float UnselectedBannerHeight = 78;

    [Export]
    public float TransitionDuration = 0.22f;

    [Export]
    public OnePlayerUI OnePlayerUI;

    private readonly List<CharacterBanner> banners = new();
    private VBoxContainer bannerContainer;
    private VBoxContainer rosterContainer;
    private ButtonPromptTextLabel previousPromptHandler;
    private ButtonPromptTextLabel nextPromptHandler;

    private sealed class CharacterBanner
    {
        public PlayerController Player;
        public HBoxContainer Row;
        public Control Banner;
        public TextureRect Artwork;
        public bool IsSelected;
        public Tween Transition;
    }

    public override void _Ready()
    {
        Color = Colors.Transparent;
        var previousLayout = GetNodeOrNull<Control>("MarginContainer");
        if (previousLayout != null)
        {
            RemoveChild(previousLayout);
            previousLayout.QueueFree();
        }

        bannerContainer = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bannerContainer.AddThemeConstantOverride("separation", 4);
        AddChild(bannerContainer);

        previousPromptHandler = CreatePromptHandler(
            "[input]actioncharaswap_l[/input]"
        );
        bannerContainer.AddChild(previousPromptHandler.RichTextLabel);

        rosterContainer = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rosterContainer.AddThemeConstantOverride("separation", 8);
        bannerContainer.AddChild(rosterContainer);

        nextPromptHandler = CreatePromptHandler(
            "[input]actioncharaswap_r[/input]"
        );
        bannerContainer.AddChild(nextPromptHandler.RichTextLabel);
    }

    public override void _Process(double delta)
    {
        if (
            PlayersManager.Instance == null
            || !PlayersManager.Instance.CharacterSwappingEnabled
            || PlayerController.Instances.Count < 2
            || OnePlayerUI == null
        )
        {
            Visible = false;
            return;
        }

        Visible = true;
        if (RosterChanged())
        {
            RebuildBanners();
        }

        var requiredHeight =
            banners.Count * (UnselectedBannerHeight + 10)
            + (SelectedBannerHeight - UnselectedBannerHeight)
            + (banners.Count - 1) * 8
            + 2 * 32
            + 2 * 4;
        OffsetTop = -requiredHeight / 2;
        OffsetBottom = requiredHeight / 2;

        if (OnePlayerUI.PlayerID < 0 || OnePlayerUI.PlayerID >= PlayerController.Instances.Count)
        {
            return;
        }

        var selectedPlayer = PlayerController.Instances[OnePlayerUI.PlayerID];
        if (OnePlayerUI.LastInputType != PlayerInput.InputType.NONE)
        {
            selectedPlayer.PlayerInput.LastInputType = OnePlayerUI.LastInputType;
        }

        BindPromptToPlayer(previousPromptHandler, selectedPlayer);
        previousPromptHandler.HandleInputTag();
        BindPromptToPlayer(nextPromptHandler, selectedPlayer);
        nextPromptHandler.HandleInputTag();

        for (var i = 0; i < banners.Count; i++)
        {
            var banner = banners[i];
            var selected = banner.Player == selectedPlayer;
            if (selected != banner.IsSelected)
            {
                AnimateBanner(banner, selected);
                banner.IsSelected = selected;
            }
        }
    }

    private bool RosterChanged()
    {
        if (banners.Count != PlayerController.Instances.Count)
        {
            return true;
        }

        for (var i = 0; i < banners.Count; i++)
        {
            if (banners[i].Player != PlayerController.Instances[i])
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildBanners()
    {
        foreach (var banner in banners)
        {
            banner.Row.QueueFree();
        }
        banners.Clear();

        for (var i = 0; i < PlayerController.Instances.Count; i++)
        {
            var player = PlayerController.Instances[i];
            var banner = CreateBanner(player);
            banners.Add(banner);
            rosterContainer.AddChild(banner.Row);
        }
    }

    private CharacterBanner CreateBanner(PlayerController player)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(350, UnselectedBannerHeight + 10),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var banner = new Control
        {
            CustomMinimumSize = new Vector2(UnselectedBannerWidth, UnselectedBannerHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(banner);

        var background = new ColorRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0, 0, 0, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        banner.AddChild(background);

        var artwork = new TextureRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(1, 1, 1, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (player.CharacterSwapIcon != null)
        {
            artwork.Texture = player.CharacterSwapIcon;
        }
        else
        {
            GD.PushWarning(
                $"No character swap icon assigned in the prefab for '{player.CharacterName}'."
            );
        }
        banner.AddChild(artwork);

        var nameLabel = new Label
        {
            Text = player.CharacterName,
            AnchorTop = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 10,
            OffsetTop = -34,
            OffsetRight = -10,
            OffsetBottom = -5,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        banner.AddChild(nameLabel);

        return new CharacterBanner
        {
            Player = player,
            Row = row,
            Banner = banner,
            Artwork = artwork,
        };
    }

    private ButtonPromptTextLabel CreatePromptHandler(string text)
    {
        var promptLabel = new RichTextLabel
        {
            CustomMinimumSize = new Vector2(350, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var promptHandler = new ButtonPromptTextLabel
        {
            Text = text,
            RichTextLabel = promptLabel,
            ControllerButtonToImageMapping = new Godot.Collections.Dictionary<string, string>
            {
                ["touch_actioncharaswap_l"] =
                    "res://SonicAssets/Images/Mobile/TouchButton_Charaswap_L.png",
                ["touch_actioncharaswap_r"] =
                    "res://SonicAssets/Images/Mobile/TouchButton_Charaswap_R.png",
            },
        };
        promptLabel.AddChild(promptHandler);
        return promptHandler;
    }

    private void BindPromptToPlayer(
        ButtonPromptTextLabel promptHandler,
        PlayerController player
    )
    {
        if (promptHandler.Player == player)
        {
            return;
        }

        if (promptHandler.Player?.PlayerInput != null)
        {
            promptHandler.Player.PlayerInput.OnInputTypeChange -=
                promptHandler.HandleInputTag;
        }

        promptHandler.Player = player;
        if (player.PlayerInput != null)
        {
            player.PlayerInput.OnInputTypeChange += promptHandler.HandleInputTag;
        }
    }

    private void AnimateBanner(CharacterBanner banner, bool selected)
    {
        banner.Transition?.Kill();
        banner.Transition = CreateTween();
        banner.Transition.SetParallel(true);
        banner.Transition.SetTrans(Tween.TransitionType.Cubic);
        banner.Transition.SetEase(Tween.EaseType.Out);

        var width = selected ? SelectedBannerWidth : UnselectedBannerWidth;
        var height = selected ? SelectedBannerHeight : UnselectedBannerHeight;
        banner.Transition.TweenProperty(
            banner.Row,
            "custom_minimum_size",
            new Vector2(350, height + 10),
            TransitionDuration
        );
        banner.Transition.TweenProperty(
            banner.Banner,
            "custom_minimum_size",
            new Vector2(width, height),
            TransitionDuration
        );
        banner.Transition.TweenProperty(
            banner.Artwork,
            "modulate:a",
            selected ? 1.0f : 0.5f,
            TransitionDuration
        );
    }
}
