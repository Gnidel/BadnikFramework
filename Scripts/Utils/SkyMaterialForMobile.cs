using Godot;
using System;

public partial class SkyMaterialForMobile : Node
{
	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public Sky MobileSky;
	[Export] public Sky HqSky;
	[Export] public bool ForceMobileVariant;	// For debugging
	public override void _Ready()
	{
		if (WorldEnvironment == null)
		{
			WorldEnvironment = this.GetParent<WorldEnvironment>();
		}
		if (WorldEnvironment == null) return;

		bool isMobile = (OS.GetName() == "Android" || OS.GetName() == "iOS");
		if ((ForceMobileVariant || isMobile) && MobileSky != null)
		{
			WorldEnvironment.Environment.Sky = MobileSky;
		}
		else if (HqSky != null)
		{
			WorldEnvironment.Environment.Sky = HqSky;
		}
	}
}
