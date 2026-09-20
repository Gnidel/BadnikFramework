using System;
using Godot;

public partial class ExtraItemTracker : HBoxContainer
{
	[Export]
	public int PlayerID;

	[Export]
	public TextureRect Icon;

	[Export]
	public RichTextLabel TextLabel;

	[Export]
	public string TrackedItem;

	public override void _PhysicsProcess(double delta)
	{
		if (PlayerID < 0 || PlayerID >= PlayerController.Instances.Count)
			return;

		int itemCount = PlayerController.Instances[PlayerID].PlayerInventory.GetItemCount(TrackedItem);
		TextLabel.Text = itemCount.ToString();
	}
}
