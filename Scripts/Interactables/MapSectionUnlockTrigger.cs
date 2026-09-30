using Godot;

public partial class MapSectionUnlockTrigger : Area3D
{
    [Export]
    public MapUIMapSection MapSection;

    [Export]
    public MapUIIconTrackedObject MapIcon;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        Monitoring = true;
        SetCollisionMaskValue(2, true);

        if (MapSection == null)
            MapSection = GetNodeOrNull<MapUIMapSection>("../MapUIMapSection");

        if (MapIcon == null)
            MapIcon = GetNodeOrNull<MapUIIconTrackedObject>("./MapIcon");

        if (MapSection == null)
            GD.PrintErr($"MapSectionUnlockTrigger '{GetPath()}' has no MapSection assigned.");

        if (MapSection?.IsUnlocked == true)
        {
            HideMapIcon();
            QueueFree();
            return;
        }
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
    }

    void OnBodyEntered(Node3D other)
    {
        if (other.GetNodeOrNull<PlayerController>(".") != null)
        {
            if (MapSection != null)
            {
                HideMapIcon();
                MapSection.Unlock();
                QueueFree();
            }
        }
    }

    void HideMapIcon()
    {
        if (MapIcon == null)
            return;

        MapIcon.QueueFree();
        MapUIControl.Instance?.CallDeferred("RefreshIcons");
    }
}