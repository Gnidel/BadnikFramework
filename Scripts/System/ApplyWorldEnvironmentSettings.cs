using Godot;
using System;

public partial class ApplyWorldEnvironmentSettings : WorldEnvironment
{
	static bool IsCompatibilityRenderer =>
		ProjectSettings.GetSetting("rendering/renderer/rendering_method").AsString() == "gl_compatibility";

	public override void _Ready()
	{
		ApplySettings();
	}

	void ApplySettings()
	{
		if (Environment == null || OptionsMenu.Options == null)
			return;

		bool compatibility = IsCompatibilityRenderer;
		Environment.SsrEnabled = !compatibility && OptionsMenu.Options.SSR > 0;
		Environment.SsaoEnabled = !compatibility && OptionsMenu.Options.AO > 0;
		Environment.SsilEnabled = !compatibility && OptionsMenu.Options.SSL > 0;
		Environment.SdfgiEnabled = !compatibility && OptionsMenu.Options.GI > 0;
		Environment.GlowEnabled = OptionsMenu.Options.Bloom > 0;
	}
}
