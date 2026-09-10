using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public partial class CharacterSelector : ScrollContainer
{
	[Export]
	public int PlayerCount = 1;

	[Export]
	public int NpcCount = 1;

	[Export]
	public bool AddEveryoneElseAsNpc = false; // Recommended to set NpcCount to 0 if you use this.

	[Export]
	public HBoxContainer Container;

	[Export]
	public VBoxContainer MenuActions;

	[Export]
	public Array<OptionButton> InputOptionButtons = new Array<OptionButton>();

	[Export]
	public string[] CharacterNames = new string[0];

	[Export]
	public PackedScene[] CharacterPrefabs = new PackedScene[0];

	[Export]
	public Texture2D[] CharacterIcons = new Texture2D[0];

	[Export]
	public Color[] CharacterThemeColors = new Color[0];

	const int KEYBOARD_INPUT_ID = 101;
	const int TOTAL_SLOTS = 4;
	const float CHARACTER_CARD_HEIGHT = 800.0f;
	const float CHARACTER_CARD_WIDTH = 260.0f;

	readonly List<int> playerSelections = new List<int>();
	readonly List<int> npcSelections = new List<int>();
	readonly List<Control> playerViewports = new List<Control>();
	readonly List<Control> npcViewports = new List<Control>();
	readonly List<Button> upButtons = new List<Button>();
	readonly List<Button> downButtons = new List<Button>();

	[Export]
	public Control PreviousMenu;

	[Export]
	public Control NextMenu;

	public override void _EnterTree()
	{
		RefreshWindow();
	}

	public void FocusFirstSelector()
	{
		CallDeferred(nameof(FocusFirstSelectorDeferred));
	}

	private void FocusFirstSelectorDeferred()
	{
		if (MenuActions == null)
			return;
		MenuActions.GetNode<Button>("PlayButton").GrabFocus();
	}

	void RefreshWindow()
	{
		MakeMenu();
	}

	private void MakeMenu()
	{
		PlayerCount = Math.Clamp(PlayerCount, 1, TOTAL_SLOTS);
		NpcCount = TOTAL_SLOTS - PlayerCount;
		MenuActions?.GetParent().MoveChild(MenuActions, 0);
		InputOptionButtons.Clear();
		playerViewports.Clear();
		npcViewports.Clear();
		upButtons.Clear();
		downButtons.Clear();
		foreach (var child in Container.GetChildren())
		{
			child.QueueFree();
		}

		Container.AddThemeConstantOverride("separation", 10);
		ResizeSelections(playerSelections, PlayerCount, 0);
		ResizeSelections(npcSelections, NpcCount, -1);

		for (int i = 0; i < PlayerCount; i++)
		{
			CreateCharacterCard("PLAYER " + (i + 1).ToString(), i, false);
		}

		for (int i = 0; i < NpcCount; i++)
		{
			CreateCharacterCard("CPU", i, true);
		}

		CallDeferred(nameof(ConfigureFocusNavigation));
	}

	private void ResizeSelections(List<int> selections, int count, int defaultValue)
	{
		while (selections.Count < count)
			selections.Add(defaultValue);
		if (selections.Count > count)
			selections.RemoveRange(count, selections.Count - count);
	}

	private void CreateCharacterCard(string title, int slot, bool npc)
	{
		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(CHARACTER_CARD_WIDTH, CHARACTER_CARD_HEIGHT + (npc || PlayerCount > 1 ? 58 : 0));
		card.FocusMode = Control.FocusModeEnum.All;
		card.AddThemeStyleboxOverride("panel", CreateCardStyle(GetCharacterColor(npc ? npcSelections[slot] : playerSelections[slot])));
		Container.AddChild(card);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		card.AddChild(layout);

		var titleLabel = new Label();
		titleLabel.Text = title;
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.AddThemeFontSizeOverride("font_size", 18);
		layout.AddChild(titleLabel);
		var up = CreateNavigationButton("▲");
		up.Pressed += () => ChangeCharacter(slot, npc, -1);
		layout.AddChild(up);
		upButtons.Add(up);

		var viewport = new Control();
		viewport.CustomMinimumSize = new Vector2(CHARACTER_CARD_WIDTH - 16, CHARACTER_CARD_HEIGHT - 104);
		viewport.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		viewport.ClipContents = true;
		viewport.MouseFilter = Control.MouseFilterEnum.Ignore;
		layout.AddChild(viewport);
		(npc ? npcViewports : playerViewports).Add(viewport);
		ShowCharacter(viewport, npc ? npcSelections[slot] : playerSelections[slot], 0, false);

		var down = CreateNavigationButton("▼");
		down.Pressed += () => ChangeCharacter(slot, npc, 1);
		layout.AddChild(down);
		downButtons.Add(down);

		if (!npc && PlayerCount > 1)
		{
			var inputOption = new OptionButton();
			inputOption.CustomMinimumSize = new Vector2(0, 30);
			inputOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			inputOption.AddThemeFontSizeOverride("font_size", 18);
			inputOption.AddItem("KEYBOARD", KEYBOARD_INPUT_ID);
			foreach (var joypad in Input.GetConnectedJoypads())
				inputOption.AddItem("PAD " + (joypad + 1).ToString(), joypad);
			if (slot < inputOption.ItemCount)
				inputOption.Selected = slot;
			InputOptionButtons.Add(inputOption);
			layout.AddChild(inputOption);
		}
	}

	private void ConfigureFocusNavigation()
	{
		if (MenuActions == null || upButtons.Count == 0)
			return;

		var play = MenuActions.GetNode<Button>("PlayButton");
		var back = MenuActions.GetNode<Button>("BackButton");
		var removePlayer = MenuActions.GetNode<Button>("RemovePlayer");
		var addPlayer = MenuActions.GetNode<Button>("AddPlayer");
		var actionButtons = new[] { play, back, removePlayer, addPlayer };
		foreach (var button in actionButtons)
			RegisterFocusVisibility(button);

		for (int i = 0; i < upButtons.Count; i++)
		{
			RegisterFocusVisibility(upButtons[i]);
			RegisterFocusVisibility(downButtons[i]);
			SetFocusNeighbor(upButtons[i], "focus_neighbor_top", upButtons[i]);
			SetFocusNeighbor(downButtons[i], "focus_neighbor_bottom", downButtons[i]);
			SetFocusNeighbor(upButtons[i], "focus_neighbor_bottom", downButtons[i]);
			SetFocusNeighbor(downButtons[i], "focus_neighbor_top", upButtons[i]);

			if (i > 0)
			{
				SetFocusNeighbor(upButtons[i], "focus_neighbor_left", upButtons[i - 1]);
				SetFocusNeighbor(downButtons[i], "focus_neighbor_left", downButtons[i - 1]);
			}
			else
			{
				SetFocusNeighbor(upButtons[i], "focus_neighbor_left", play);
				SetFocusNeighbor(downButtons[i], "focus_neighbor_left", play);
			}
			if (i + 1 < upButtons.Count)
			{
				SetFocusNeighbor(upButtons[i], "focus_neighbor_right", upButtons[i + 1]);
				SetFocusNeighbor(downButtons[i], "focus_neighbor_right", downButtons[i + 1]);
			}
		}

		for (int i = 0; i < InputOptionButtons.Count; i++)
		{
			var inputOption = InputOptionButtons[i];
			RegisterFocusVisibility(inputOption);
			SetFocusNeighbor(downButtons[i], "focus_neighbor_bottom", inputOption);
			SetFocusNeighbor(inputOption, "focus_neighbor_top", downButtons[i]);
			SetFocusNeighbor(inputOption, "focus_neighbor_left", downButtons[i]);
			if (i + 1 < InputOptionButtons.Count)
				SetFocusNeighbor(inputOption, "focus_neighbor_right", InputOptionButtons[i + 1]);
			else
				SetFocusNeighbor(inputOption, "focus_neighbor_right", downButtons[i]);
		}

		for (int i = 0; i < actionButtons.Length; i++)
		{
			SetFocusNeighbor(actionButtons[i], "focus_neighbor_right", upButtons[0]);
			if (i > 0)
				SetFocusNeighbor(actionButtons[i], "focus_neighbor_top", actionButtons[i - 1]);
			if (i + 1 < actionButtons.Length)
				SetFocusNeighbor(actionButtons[i], "focus_neighbor_bottom", actionButtons[i + 1]);
		}
	}

	private void RegisterFocusVisibility(Control control)
	{
		control.FocusEntered += () => EnsureControlVisible(control);
	}

	private void SetFocusNeighbor(Control control, string property, Control neighbor)
	{
		if (!IsInstanceValid(control) || !IsInstanceValid(neighbor) || !control.IsInsideTree() || !neighbor.IsInsideTree())
			return;
		control.Set(property, control.GetPathTo(neighbor));
	}

	private Button CreateNavigationButton(string text)
	{
		var button = new Button();
		button.Text = text;
		button.CustomMinimumSize = new Vector2(CHARACTER_CARD_WIDTH - 16, 30);
		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		button.FocusMode = Control.FocusModeEnum.All;
		return button;
	}

	private StyleBoxFlat CreateCardStyle(Color color)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(color, 0.18f);
		style.BorderColor = new Color(color, 0.75f);
		style.SetBorderWidthAll(2);
		style.ContentMarginLeft = 10;
		style.ContentMarginRight = 10;
		style.ContentMarginTop = 8;
		style.ContentMarginBottom = 8;
		return style;
	}

	private Color GetCharacterColor(int characterIndex)
	{
		if (characterIndex < 0 || characterIndex >= CharacterThemeColors.Length)
			return new Color(0.18f, 0.18f, 0.22f);
		return CharacterThemeColors[characterIndex];
	}

	private void ChangeCharacter(int slot, bool npc, int direction)
	{
		var selections = npc ? npcSelections : playerSelections;
		var viewport = npc ? npcViewports[slot] : playerViewports[slot];
		var previous = selections[slot];
		var count = npc ? CharacterNames.Length + 1 : CharacterNames.Length;
		var next = (previous + direction + count) % count;
		selections[slot] = npc && next == CharacterNames.Length ? -1 : next;
		ShowCharacter(viewport, selections[slot], direction, true);
		var card = viewport.GetParent().GetParent() as PanelContainer;
		if (card != null)
			card.AddThemeStyleboxOverride("panel", CreateCardStyle(GetCharacterColor(selections[slot])));
	}

	private void ShowCharacter(Control viewport, int characterIndex, int direction, bool animate)
	{
		var content = new VBoxContainer();
		content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		content.Position = new Vector2(0, animate ? direction * viewport.Size.Y : 0);
		content.Alignment = BoxContainer.AlignmentMode.Center;
		content.MouseFilter = Control.MouseFilterEnum.Ignore;
		viewport.AddChild(content);

		var icon = new TextureRect();
		icon.CustomMinimumSize = new Vector2(0, 125);
		icon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		icon.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		icon.Texture = characterIndex >= 0 && characterIndex < CharacterIcons.Length ? CharacterIcons[characterIndex] : null;
		content.AddChild(icon);

		var name = new Label();
		name.Text = characterIndex >= 0 && characterIndex < CharacterNames.Length ? CharacterNames[characterIndex].ToUpper() : "NONE";
		name.HorizontalAlignment = HorizontalAlignment.Center;
		name.AddThemeFontSizeOverride("font_size", 22);
		content.AddChild(name);

		if (!animate)
			return;

		foreach (var child in viewport.GetChildren())
		{
			if (child != content)
			{
				var oldContent = child as Control;
				var oldTween = oldContent.CreateTween();
				oldTween.TweenProperty(oldContent, "position:y", -direction * viewport.Size.Y, 0.22f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
				oldTween.TweenCallback(Callable.From(oldContent.QueueFree));
			}
		}

		var tween = content.CreateTween();
		tween.TweenProperty(content, "position:y", 0.0f, 0.22f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	void OnPlay()
	{
		ApplyCharacters();
		this.Visible = false;
		NextMenu.Visible = true;
		NextMenu.FindNextValidFocus().GrabFocus();
	}

	void OnBack()
	{
		this.Visible = false;
		PreviousMenu.Visible = true;
		PreviousMenu.FindNextValidFocus().GrabFocus();
	}

	void ApplyCharacters()
	{
		PlayersManager.PlayablePlayers = new Array<PackedScene>();
		PlayersManager.NpcPlayers = new Array<PackedScene>();
		PlayersManager.PlayerIdentifiers = new Array<string>();
		foreach (var selectedPlayer in playerSelections)
		{
			PlayersManager.PlayablePlayers.Add(CharacterPrefabs[selectedPlayer]);
		}

		if (PlayerCount == 1)
		{
			PlayersManager.PlayerIdentifiers.Add("any");
		}
		else
		{
			foreach (var inputOption in InputOptionButtons)
			{
				var selectedInput = inputOption.GetSelectedId();
				if (selectedInput == KEYBOARD_INPUT_ID)
				{
					PlayersManager.PlayerIdentifiers.Add("kb");
				}
				else
				{
					PlayersManager.PlayerIdentifiers.Add("pad" + selectedInput.ToString());
				}
			}
		}

		foreach (var selectedPlayer in npcSelections)
		{
			if (selectedPlayer < 0)
				continue;

			PlayersManager.NpcPlayers.Add(CharacterPrefabs[selectedPlayer]);
		}

		if (AddEveryoneElseAsNpc)
		{
			for (int i = 0; i < CharacterPrefabs.Length; i++)
			{
				bool foundAmongPlayers = false;
				foreach (var selectedPlayer in playerSelections)
				{
					if (selectedPlayer == i)
					{
						foundAmongPlayers = true;
						break;
					}
				}
				if (!foundAmongPlayers)
				{
					PlayersManager.NpcPlayers.Add(CharacterPrefabs[i]);
				}
			}
		}
	}

	void ChangePlayers(int playersDiff)
	{
		PlayerCount = Math.Clamp(PlayerCount + playersDiff, 1, TOTAL_SLOTS);
		NpcCount = TOTAL_SLOTS - PlayerCount;
		RefreshWindow();
	}
}
