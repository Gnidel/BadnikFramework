using System;
using Godot;

public partial class ExtraItemTracker : HBoxContainer
{
	[Export]
	public TextureRect Icon;

	[Export]
	public RichTextLabel TextLabel;

	[Export]
	public string TrackedItem;

	public override void _PhysicsProcess(double delta)
	{
		int itemCount = 0;
		foreach (var player in PlayerController.Instances)
		{
			itemCount += player.PlayerInventory.GetItemCount(TrackedItem);
		}
		TextLabel.Text = itemCount.ToString();
	}
}
