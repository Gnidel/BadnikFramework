using System.Collections.Generic;
using Godot;

public partial class SaveSelector : VBoxContainer
{
	[Export]
	public HBoxContainer SaveFilesContainer;

	[Export]
	public ScrollContainer SaveFilesScrollContainer;

	[Export]
	public VBoxContainer NewSaveColumn;

	[Export]
	public Button NewSaveButton;

	private ConfirmationDialog removeConfirmationDialog;
	private int pendingRemoveSaveId;
	private Button pendingRemoveButton;

	public override void _Ready()
	{
		removeConfirmationDialog = new ConfirmationDialog
		{
			Title = "",
			DialogText = "Are you sure you want to remove this save?"
		};
		removeConfirmationDialog.GetOkButton().Text = "YES";
		removeConfirmationDialog.GetCancelButton().Text = "NO";
		removeConfirmationDialog.Confirmed += ConfirmRemoveSave;
		removeConfirmationDialog.Canceled += CancelRemoveSave;
		AddChild(removeConfirmationDialog);

		NewSaveButton.Pressed += CreateSave;
		NewSaveButton.FocusEntered += () => RevealFocusedControl(NewSaveButton);
		Refresh();
	}

	public void Refresh()
	{
		foreach (var child in SaveFilesContainer.GetChildren())
		{
			if (child == NewSaveColumn)
				continue;
			SaveFilesContainer.RemoveChild(child);
			child.QueueFree();
		}

		var saveIds = SaveData.GetSaveFileIds();
		SaveFilesContainer.AddThemeConstantOverride("separation", 16);
		foreach (var saveId in saveIds)
		{
			var background = new ColorRect
			{
				CustomMinimumSize = new Vector2(300, 220),
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Color = new Color(0, 0, 0, 0.5f)
			};

			var column = new VBoxContainer();
			column.Alignment = BoxContainer.AlignmentMode.Begin;

			var label = new Label { Text = "SAVE " + (saveId + 1).ToString() };
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			var actions = new VBoxContainer();
			actions.Alignment = BoxContainer.AlignmentMode.Center;
			var loadButton = new Button { Text = "LOAD" };
			var removeButton = new Button { Text = "REMOVE" };
			var capturedSaveId = saveId;
			loadButton.CustomMinimumSize = new Vector2(-1, 200);
			loadButton.Pressed += () => LoadSave(capturedSaveId);
			removeButton.Pressed += () => RequestRemoveSave(capturedSaveId, removeButton);
			loadButton.FocusEntered += () => RevealFocusedControl(loadButton);
			removeButton.FocusEntered += () => RevealFocusedControl(removeButton);

			background.AddChild(column);
			column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			actions.AddChild(loadButton);
			actions.AddChild(removeButton);
			column.AddChild(label);
			column.AddChild(actions);
			SaveFilesContainer.AddChild(background);
		}
		SaveFilesContainer.MoveChild(NewSaveColumn, SaveFilesContainer.GetChildCount() - 1);

		if (saveIds.Count > 0)
		{
			SaveFilesContainer
				.GetChild<ColorRect>(0)
				.GetChild<VBoxContainer>(0)
				.GetChild<VBoxContainer>(1)
				.GetChild<Button>(0)
				.CallDeferred(Control.MethodName.GrabFocus);
		}
		else
		{
			NewSaveButton.GrabFocus();
		}
	}

	private void RevealFocusedControl(Control control)
	{
		SaveFilesScrollContainer.EnsureControlVisible(control);
	}

	private void CreateSave()
	{
		var saveId = SaveData.GetNextSaveFileId();
		SaveData.Erase(saveId);
		LoadSave(saveId);
	}

	private void LoadSave(int saveId)
	{
		MainMenuController.Instance.SelectSave(saveId);
	}

	private void RequestRemoveSave(int saveId, Button removeButton)
	{
		pendingRemoveSaveId = saveId;
		pendingRemoveButton = removeButton;
		removeConfirmationDialog.PopupCentered();
	}

	private void ConfirmRemoveSave()
	{
		SaveData.Delete(pendingRemoveSaveId);
		removeConfirmationDialog.Hide();
		Refresh();
	}

	private void CancelRemoveSave()
	{
		removeConfirmationDialog.Hide();
		pendingRemoveButton?.CallDeferred(Control.MethodName.GrabFocus);
	}
}
