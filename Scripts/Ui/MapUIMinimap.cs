using Godot;

public partial class MapUIMinimap : Control
{
    [Export]
    public SubViewportContainer MinimapViewportContainer;

    [Export]
    public SubViewport MinimapViewport;

    [Export]
    public MapUIMinimapViewport MinimapContent;

    [Export]
    public Label NorthLabel;

    [Export]
    public float WorldRadius = 100f;

    [Export]
    public bool RotateIconsWithMinimap = false;

    public override void _Ready()
    {
        MinimapContent.Position = new Vector2(
            MinimapViewport.Size.X / 2f,
            MinimapViewport.Size.Y / 2f
        );
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        bool mapOpen =
            MapUIControl.Instance != null
            && GodotObject.IsInstanceValid(MapUIControl.Instance.MapUI)
            && MapUIControl.Instance.MapUI.Visible;
        Visible = !mapOpen;

        var center = Size / 2f;
        var northDirection = Vector2.Up.Rotated(MinimapContent.MapRotation);
        var northLabelRadius = Mathf.Min(Size.X, Size.Y) / 2f - 15f;
        NorthLabel.Position =
            center + northDirection * northLabelRadius - NorthLabel.Size / 2f;

        MinimapContent.QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2f;
        DrawCircle(center, Mathf.Min(Size.X, Size.Y) / 2f, new Color(0.02f, 0.04f, 0.08f, 0.5f));
    }
}
