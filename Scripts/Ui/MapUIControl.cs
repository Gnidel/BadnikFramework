using System;
using System.Collections.Generic;
using System.Drawing;
using Godot;
using static Godot.LineEdit;

public partial class MapUIControl : Node
{
    public static MapUIControl Instance;

    [Export]
    public Control MapUI;

    [Export]
    public Texture2D PlayerIconTexture;

    [Export]
    public float MapScaleDefault = 5f;

    [Export]
    public float MapScaleMin = 1f;

    [Export]
    public float MapScaleMax = 5f;

    List<PlayerIcon> PlayerIcons = new List<PlayerIcon>();
    List<POIIcon> PoiIcons = new List<POIIcon>();

    [Export]
    public Node2D IconsContainer;

    [Export]
    public Sprite2D Cursor;

    [Export]
    public Control MapRoot;

    [Export]
    public Control MapViewport;

    [Export]
    public TextureRect MapTerrain;

    [Export]
    public Button FastTravelButton;

    [Export]
    public Button CloseButton;

    [Export]
    public Aabb MapBorders = new Aabb(-100, 0, -100, 200, 0, 200);

    List<POIIcon> selectedPoiIcons = new List<POIIcon>();

    public List<Vector3> StageLocations = new List<Vector3>();

    [Export]
    public float MoveMapAfterCursorDistance = 100; // Note: this scales with the map scale

    [Export]
    public float MapMoveSpeed = 0.5f; // It's not a cursor movement speed, but map move to cursor speed

    [Export]
    public float FasterCursorMoveMultiplier = 3f;
    public Vector2 MapOffset = Vector2.Zero;

    readonly Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();
    Vector2 touchStartPosition;
    bool touchMoved;
    float pinchDistance;
    bool mobileMapGestureActive;

    [Export]
    public RichTextLabel DescriptionHeader;

    [Export]
    public RichTextLabel DescriptionContent;

    class PlayerIcon
    {
        public PlayerController player;
        public Sprite2D sprite;
    }

    class POIIcon
    {
        public MapUIIconTrackedObject trackedObject;
        public Sprite2D sprite;
    }

    public void AddPoi(MapUIIconTrackedObject trackedObject)
    {
        // Sprite
        var newIconSprite = new Sprite2D();
        newIconSprite.Name = trackedObject.Name + "_icon";
        newIconSprite.Texture = trackedObject.Texture;
        newIconSprite.Position = new Vector2(
            trackedObject.GlobalPosition.X,
            trackedObject.GlobalPosition.Z
        );
        IconsContainer.AddChild(newIconSprite);

        // Add to collection
        POIIcon icon = new POIIcon();
        icon.trackedObject = trackedObject;
        icon.sprite = newIconSprite;
        PoiIcons.Add(icon);
    }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Ready()
    {
        RefreshIcons();
        MapRoot.Scale = new Vector2(MapScaleDefault, MapScaleDefault);
    }

    public override void _Input(InputEvent @event)
    {
        if (!MapUI.Visible)
            return;

        if (
            @event is InputEventMouseButton mouseButton
            && mouseButton.Pressed
            && MapViewport.GetGlobalRect().HasPoint(mouseButton.Position)
        )
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                SetMapScale(MapRoot.Scale.X + 0.5f);
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                SetMapScale(MapRoot.Scale.X - 0.5f);
            else
                return;

            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                if (!MapViewport.GetGlobalRect().HasPoint(touch.Position))
                    return;

                activeTouches[touch.Index] = touch.Position;
                touchStartPosition = touch.Position;
                touchMoved = false;
                mobileMapGestureActive = true;

                if (activeTouches.Count == 2)
                {
                    pinchDistance = GetTouchDistance();
                    touchMoved = true;
                }
            }
            else
            {
                bool wasSingleTouch = activeTouches.Count == 1;
                activeTouches.Remove(touch.Index);

                if (wasSingleTouch && !touchMoved)
                    Cursor.GlobalPosition = touch.Position;

                if (activeTouches.Count < 2)
                    pinchDistance = 0;
                if (activeTouches.Count == 0)
                    mobileMapGestureActive = false;
            }

            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventScreenDrag drag && activeTouches.ContainsKey(drag.Index))
        {
            activeTouches[drag.Index] = drag.Position;

            if ((drag.Position - touchStartPosition).LengthSquared() > 64)
                touchMoved = true;

            if (activeTouches.Count == 1)
            {
                mobileMapGestureActive = false;
                Cursor.Position -= drag.ScreenRelative / MapRoot.Scale;
            }
            else if (activeTouches.Count == 2)
            {
                var newPinchDistance = GetTouchDistance();
                if (pinchDistance > 0 && newPinchDistance > 0)
                {
                    var scaleFactor = newPinchDistance / pinchDistance;
                    SetMapScale(MapRoot.Scale.X * scaleFactor);
                }
                pinchDistance = newPinchDistance;
            }

            GetViewport().SetInputAsHandled();
        }
    }

    float GetTouchDistance()
    {
        var touchPositions = new List<Vector2>(activeTouches.Values);
        return touchPositions[0].DistanceTo(touchPositions[1]);
    }

    void SetMapScale(float scale)
    {
        var clampedScale = Mathf.Clamp(scale, MapScaleMin, MapScaleMax);
        var cursorGlobalPosition = Cursor.GlobalPosition;
        MapRoot.Scale = Vector2.One * clampedScale;
        MapRoot.GlobalPosition += cursorGlobalPosition - Cursor.GlobalPosition;
    }

    //  public override void _Input(InputEvent @event)
    //  {
    //if (!this.MapUI.Visible) return;
    //      if (Input.IsActionJustPressed("any_actionjump")){
    //	this.OnTeleport();
    //}
    //  }

    public void RefreshIcons()
    {
        var currentIcons = IconsContainer.GetChildren();

        foreach (var child in currentIcons)
        {
            child.QueueFree();
        }

        PlayerIcons.Clear();
        foreach (var player in PlayerController.Instances)
        {
            var newIcon = new Sprite2D();
            newIcon.Name = player.Name;
            newIcon.Texture = PlayerIconTexture;
            newIcon.Position = new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
            newIcon.ZIndex = 128;
            IconsContainer.AddChild(newIcon);

            PlayerIcon iconData = new PlayerIcon();
            iconData.sprite = newIcon;
            iconData.player = player;
            PlayerIcons.Add(iconData);
        }

        PoiIcons.Clear();
        foreach (var trackedObject in MapUIIconTrackedObject.Instances)
        {
            AddPoi(trackedObject);
        }
    }

    public override void _Process(double delta)
    {
        // Input
        var LeftRawInput = Input.GetVector(
            "any_moveleft",
            "any_moveright",
            "any_moveup",
            "any_movedown"
        );
        var RightRawInput = Input.GetVector(
            "any_camleft",
            "any_camright",
            "any_camup",
            "any_camdown"
        );

        // Cursor movement
        float cursorSpeedMultiplier = 1;
        if (Input.IsActionPressed("any_actiondash"))
        {
            cursorSpeedMultiplier = FasterCursorMoveMultiplier;
        }
        Cursor.Position +=
            LeftRawInput * (float)delta * 500f * cursorSpeedMultiplier / MapRoot.Scale;
        // Apply cursor limit
        if (Cursor.Position.X < MapBorders.Position.X)
            Cursor.Position = new Vector2(MapBorders.Position.X, Cursor.Position.Y);
        if (Cursor.Position.X > MapBorders.Position.X + MapBorders.Size.X)
            Cursor.Position = new Vector2(
                MapBorders.Position.X + MapBorders.Size.X,
                Cursor.Position.Y
            );

        if (Cursor.Position.Y < MapBorders.Position.Z)
            Cursor.Position = new Vector2(Cursor.Position.X, MapBorders.Position.Z);
        if (Cursor.Position.Y > MapBorders.Position.Z + MapBorders.Size.Z)
            Cursor.Position = new Vector2(
                Cursor.Position.X,
                MapBorders.Position.Z + MapBorders.Size.Z
            );

        // Scale
        SetMapScale(MapRoot.Scale.X + -RightRawInput.Y * (float)delta * 1.1f);

        // Move map if cursor is beyond a certain distance
        var mapOffsetToCursor =
            ((-Cursor.Position * MapRoot.Scale) + MapRoot.GetParent<Control>().Size / 2)
            - MapRoot.Position;
        if (!mobileMapGestureActive && mapOffsetToCursor.Length() > MoveMapAfterCursorDistance)
        {
            //MapRoot.Position += mapOffsetToCursor.Normalized() * (float)delta * MapMoveSpeed;
            MapRoot.Position = MapRoot.Position.Lerp(
                (-Cursor.Position * MapRoot.Scale) + MapRoot.GetParent<Control>().Size / 2,
                Mathf.Min(
                    (float)delta
                        * MapMoveSpeed
                        * mapOffsetToCursor.Length()
                        / MoveMapAfterCursorDistance,
                    1
                )
            );
        }

        // Map BG adjustments
        MapTerrain.Position = new Vector2(MapBorders.Position.X, MapBorders.Position.Z);
        MapTerrain.Size = new Vector2(MapBorders.Size.X, MapBorders.Size.Z);

        MapTerrain.StretchMode = TextureRect.StretchModeEnum.Scale;
        MapTerrain.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;

        // Prepare icons and scale
        foreach (var icon in PlayerIcons)
        {
            icon.sprite.Position = new Vector2(
                icon.player.GlobalPosition.X,
                icon.player.GlobalPosition.Z
            );
            icon.sprite.Scale = Vector2.One / MapRoot.Scale;
            icon.sprite.Rotation = -icon.player.GlobalRotation.Y;
        }

        foreach (var icon in PoiIcons)
        {
            icon.sprite.Position = new Vector2(
                icon.trackedObject.GlobalPosition.X,
                icon.trackedObject.GlobalPosition.Z
            );
            icon.sprite.Scale = Vector2.One / MapRoot.Scale;
        }

        Cursor.Scale = Vector2.One / MapRoot.Scale;

        // Selected icon choosing
        selectedPoiIcons.Clear();

        foreach (var icon in PoiIcons)
        {
            if ((icon.sprite.GlobalPosition - Cursor.GlobalPosition).Length() < 50)
            {
                selectedPoiIcons.Add(icon);
            }
        }

        // Attract cursor to the selected icon
        if (selectedPoiIcons.Count == 1)
        {
            if (LeftRawInput.Length() < 0.25f)
            {
                Cursor.GlobalPosition = Cursor.GlobalPosition.Lerp(
                    selectedPoiIcons[0].sprite.GlobalPosition,
                    (float)delta * 10
                );
            }
        }

        // Read data from the selected icon to enable fast travel
        if (
            selectedPoiIcons.Count != 1
            || selectedPoiIcons[0].trackedObject.FastTravelPoint == null
        )
        {
            FastTravelButton.Disabled = true;
            FastTravelButton.ReleaseFocus();
        }
        else if (selectedPoiIcons.Count == 1)
        {
            bool previouslyDisabledButton = FastTravelButton.Disabled;
            FastTravelButton.Disabled = false;
            if (previouslyDisabledButton)
            {
                FastTravelButton.GrabFocus();
            }
        }

        // Descriptions
        if (selectedPoiIcons.Count == 0)
        {
            DescriptionContent.Text = "";
        }
        else if (selectedPoiIcons.Count == 1)
        {
            DescriptionContent.Text = selectedPoiIcons[0].trackedObject.Description;
        }
        else
        {
            DescriptionContent.Text = "Multiple icons nearby. Please zoom in.";
        }

        //MapRoot.GlobalPosition = MapRoot.GetParent<Control>().Size / 2;
        //var MapBorders2D = new Vector2(MapBorders.Size.X, MapBorders.Size.Z);
        //MapRoot.Scale = MapBorders2D / MapRoot.GetParent<Control>().Size;
    }

    public void OnTeleport()
    {
        if (
            selectedPoiIcons.Count == 0
            || selectedPoiIcons[0].trackedObject.FastTravelPoint == null
        )
            return;

        var trackedObject = selectedPoiIcons[0].trackedObject;

        foreach (var player in PlayerController.Instances)
        {
            player.ProcessMode = ProcessModeEnum.Disabled;
            player.GlobalPosition = trackedObject.FastTravelPoint.GlobalPosition;
            player.GlobalRotation = trackedObject.FastTravelPoint.GlobalRotation;
            player.LinearVelocity = Vector3.Zero;

            var cam = PlayerCamera.Instances.GetValueOrDefault(player.PlayerID, null);
            if (cam != null)
            {
                cam.GlobalPosition =
                    trackedObject.FastTravelPoint.GlobalPosition
                    + trackedObject.FastTravelPoint.GlobalRotation
                        * (cam.CameraOffset + new Vector3(0, 0, cam.MaxCameraDistance));
                cam.GlobalRotation = trackedObject.FastTravelPoint.GlobalRotation;
            }
            player.ProcessMode = ProcessModeEnum.Inherit;
        }

        PauseMenu.Instance.Unpause();
    }

    public void OnClose()
    {
        PauseMenu.Instance.Unpause();
    }
}
