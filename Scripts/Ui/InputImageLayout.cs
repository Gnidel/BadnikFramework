using Godot;
using Godot.Collections;

[GlobalClass]
public partial class InputImageLayout : Resource
{
    [Export]
    public Dictionary<string, string> Mapping = new Dictionary<string, string>();
}
