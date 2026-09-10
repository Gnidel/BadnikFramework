using Godot;
using System;

public partial class SkateRemoverTrigger : Area3D
{
	public void OnBodyEnter(Node3D other)
	{
		var player = other.GetNodeOrNull<PlayerController>(".");
		if (player != null)
		{
			var actionSkate = player.GetNodeOrNull<ActionSkate>(
                "./PlayerControl/Actions/ActionSkate"
			);
			if (actionSkate != null && actionSkate.IsSkating)
			{
				actionSkate.StopSkating();
				player.PlayerInventory.SetItemCount("skateboard", 0, false);
			}
		}
	}
}
