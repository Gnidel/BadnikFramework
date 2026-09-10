#if TOOLS
using Godot;

namespace BadnikFramework.Tools.HsonImport
{
    // Registers HsonImportNode as a proper, searchable node type (Scene > Add Child Node)
    // instead of requiring users to attach the script to a plain Node by hand.
    [Tool]
    public partial class HsonImportPlugin : EditorPlugin
    {
        private const string TypeName = "HsonImportNode";
        private const string BaseType = "Node";
        private const string ScriptPath = "res://addons/hson_import/HsonImportNode.cs";
        private const string IconPath = "res://addons/hson_import/icon.svg";

        public override void _EnterTree()
        {
            var script = GD.Load<Script>(ScriptPath);
            var icon = GD.Load<Texture2D>(IconPath);
            AddCustomType(TypeName, BaseType, script, icon);
        }

        public override void _ExitTree()
        {
            RemoveCustomType(TypeName);
        }
    }
}
#endif
