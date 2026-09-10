#if TOOLS
using Godot;

[Tool]
public partial class SkyBakerPlugin : EditorPlugin
{
	public static EditorUndoRedoManager UndoRedo { get; private set; }

	private const string TypeName = "SkyBaker";
	private const string BaseType = "Node3D";
	private const string ScriptPath = "res://addons/sky_baker/SkyBaker.cs";

	public override void _EnterTree()
	{
		UndoRedo = GetUndoRedo();
		Script script = GD.Load<Script>(ScriptPath);
		AddCustomType(TypeName, BaseType, script, null);
	}

	public override void _ExitTree()
	{
		RemoveCustomType(TypeName);
		if (UndoRedo != null)
			UndoRedo = null;
	}
}
#endif
