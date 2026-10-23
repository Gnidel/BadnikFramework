using System.Collections.Generic;
using Godot;

public partial class MapUIMinimapViewport : Node2D
{
    const float IconSize = 18f;
    const float PlayerIconSize = 24f;

    readonly Dictionary<ulong, Sprite2D> poiSprites = new();
    readonly HashSet<ulong> livePoiIds = new();
    readonly List<ulong> stalePoiIds = new();

    float mapRotation;
    Sprite2D mapSprite;
    Sprite2D playerSprite;

    public float MapRotation => mapRotation;

    public override void _Ready()
    {
        mapSprite = new Sprite2D
        {
            Name = "MapTerrain",
            ZIndex = -1,
            Centered = true,
        };
        AddChild(mapSprite);

        playerSprite = new Sprite2D
        {
            Name = "PlayerIcon",
            ZIndex = 2,
            Centered = true,
        };
        AddChild(playerSprite);
    }

    public override void _Process(double delta)
    {
        var mapControl = MapUIControl.Instance;
        PlayerController player = null;
        if (mapControl != null)
        {
            foreach (var candidate in PlayerController.Instances)
            {
                if (GodotObject.IsInstanceValid(candidate))
                {
                    player = candidate;
                    break;
                }
            }
        }

        if (mapControl == null || player == null)
        {
            playerSprite.Visible = false;
            foreach (var sprite in poiSprites.Values)
                sprite.Visible = false;
            QueueRedraw();
            return;
        }

        PlayerCamera.Instances.TryGetValue(player.PlayerID, out var camera);
        var heading = GetHeading(player, camera);
        mapRotation = mapControl.KeepNorthUp ? 0f : -heading;
        var playerHeading = GetPlayerHeading(player);
        var mapRadius = Mathf.Min(GetViewportRect().Size.X, GetViewportRect().Size.Y) / 2f;
        var visibleRadius = Mathf.Max(0f, mapRadius - IconSize / 2f);
        var minimap = GetParent().GetParent().GetParent<MapUIMinimap>();
        var worldRadius = Mathf.Max(0.01f, minimap.WorldRadius);
        var pixelsPerWorldUnit = visibleRadius / worldRadius;
        var bounds = mapControl.MapBorders;

        mapSprite.Texture = mapControl.MapTerrain?.Texture;
        mapSprite.Visible =
            mapSprite.Texture != null
            && bounds.Size.X > 0
            && bounds.Size.Z > 0;
        mapSprite.Rotation = mapRotation;
        if (mapSprite.Texture != null && mapSprite.Visible)
        {
            var mapSize = new Vector2(
                bounds.Size.X * pixelsPerWorldUnit,
                bounds.Size.Z * pixelsPerWorldUnit
            );
            var mapTopLeft = new Vector2(
                (bounds.Position.X - player.GlobalPosition.X) * pixelsPerWorldUnit,
                (bounds.Position.Z - player.GlobalPosition.Z) * pixelsPerWorldUnit
            );
            mapSprite.Position = (mapTopLeft + mapSize / 2f).Rotated(mapRotation);
            var textureSize = mapSprite.Texture.GetSize();
            if (textureSize.X > 0 && textureSize.Y > 0)
            {
                mapSprite.Scale = new Vector2(
                    mapSize.X / textureSize.X,
                    mapSize.Y / textureSize.Y
                );
            }
        }

        playerSprite.Texture = mapControl.PlayerIconTexture;
        playerSprite.Visible = playerSprite.Texture != null;
        playerSprite.Rotation = playerHeading + mapRotation;
        SetSpriteSize(playerSprite, PlayerIconSize);

        livePoiIds.Clear();
        foreach (var trackedObject in MapUIIconTrackedObject.Instances)
        {
            if (!GodotObject.IsInstanceValid(trackedObject))
                continue;

            var instanceId = trackedObject.GetInstanceId();
            livePoiIds.Add(instanceId);
            if (
                trackedObject.Texture == null
                || IsPointLocked(mapControl, trackedObject.GlobalPosition)
            )
            {
                HidePoiSprite(instanceId);
                continue;
            }

            var offset = new Vector2(
                trackedObject.GlobalPosition.X - player.GlobalPosition.X,
                trackedObject.GlobalPosition.Z - player.GlobalPosition.Z
            ).Rotated(mapRotation) * pixelsPerWorldUnit;
            var distance = offset.Length();
            if (distance > visibleRadius)
            {
                if (!trackedObject.IsTracked)
                {
                    HidePoiSprite(instanceId);
                    continue;
                }

                offset = offset.Normalized() * visibleRadius;
            }

            if (!poiSprites.TryGetValue(instanceId, out var sprite))
            {
                sprite = new Sprite2D
                {
                    Name = trackedObject.Name + "_MinimapIcon",
                    Centered = true,
                    ZIndex = 1,
                };
                poiSprites.Add(instanceId, sprite);
                AddChild(sprite);
            }

            sprite.Texture = trackedObject.Texture;
            sprite.Position = offset;
            sprite.Rotation = minimap.RotateIconsWithMinimap ? mapRotation : 0f;
            sprite.Visible = true;
            SetSpriteSize(sprite, IconSize);
        }

        stalePoiIds.Clear();
        foreach (var entry in poiSprites)
        {
            if (!livePoiIds.Contains(entry.Key))
            {
                entry.Value.QueueFree();
                stalePoiIds.Add(entry.Key);
            }
        }
        foreach (var instanceId in stalePoiIds)
            poiSprites.Remove(instanceId);

        QueueRedraw();
    }

    static void SetSpriteSize(Sprite2D sprite, float size)
    {
        if (sprite.Texture == null)
            return;

        var textureSize = sprite.Texture.GetSize();
        var maxDimension = Mathf.Max(textureSize.X, textureSize.Y);
        if (maxDimension > 0)
            sprite.Scale = Vector2.One * (size / maxDimension);
    }

    void HidePoiSprite(ulong instanceId)
    {
        if (poiSprites.TryGetValue(instanceId, out var sprite))
            sprite.Visible = false;
    }

    static float GetHeading(PlayerController player, PlayerCamera camera)
    {
        if (camera != null && GodotObject.IsInstanceValid(camera))
        {
            var forward = -camera.GlobalBasis.Z;
            forward.Y = 0;
            if (forward.LengthSquared() > 0.0001f)
            {
                forward = forward.Normalized();
                return Mathf.Atan2(forward.X, -forward.Z);
            }
        }

        return GetPlayerHeading(player);
    }

    static float GetPlayerHeading(PlayerController player)
    {
        var forward = -player.GlobalBasis.Z;
        forward.Y = 0;
        if (forward.LengthSquared() > 0.0001f)
        {
            forward = forward.Normalized();
            return Mathf.Atan2(forward.X, -forward.Z);
        }

        return -player.GlobalRotation.Y;
    }

    static bool IsPointLocked(MapUIControl mapControl, Vector3 worldPosition)
    {
        var mapPosition = new Vector2(worldPosition.X, worldPosition.Z);
        foreach (var section in MapUIMapSection.Instances)
        {
            if (GodotObject.IsInstanceValid(section) && section.IsPointLocked(mapPosition))
                return true;
        }

        return false;
    }
}
