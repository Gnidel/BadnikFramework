using Godot;

[GlobalClass]
public partial class RequiredItem : Resource
{
    [Export]
    public string Id;

    [Export]
    public string VisibleName;

    [Export]
    public int Count;

    [Export]
    public Texture2D Icon;
}
