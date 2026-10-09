using System.Collections.Generic;
using Godot;

public partial class MapUIMapSection : Node3D
{
    public static readonly List<MapUIMapSection> Instances = new();

    private Texture2D _cachedMaskTexture;
    private Image _maskImage;
    private bool _maskImageLoaded;

    [Export]
    public Texture2D MaskTexture;   // White = locked, Black = unlocked

    [Export]
    public string UnlockId;

    [Export]
    public float LockedIconThreshold = 0.5f;

    public bool IsUnlocked { get; private set; }

    public bool IsRevealing { get; set; }

    public override void _EnterTree()
    {
        Instances.Add(this);
        IsUnlocked = ReadUnlockedState();
        MapUIControl.Instance?.RefreshMapSections();
        MapUIControl.Instance?.RefreshIcons();
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
        MapUIControl.Instance?.RefreshMapSections();
        MapUIControl.Instance?.RefreshIcons();
    }

    public string GetPersistentId()
    {
        if (!string.IsNullOrEmpty(UnlockId))
            return UnlockId;

        return (GetTree().CurrentScene?.SceneFilePath ?? "") + "\\" + GetPath();
    }

    public bool IsPointLocked(Vector2 mapPosition)
    {
        if ((IsUnlocked && !IsRevealing) || MaskTexture == null)
            return false;

        if (_cachedMaskTexture != MaskTexture)
        {
            _cachedMaskTexture = MaskTexture;
            _maskImage = null;
            _maskImageLoaded = false;
        }

        if (!_maskImageLoaded)
        {
            _maskImage = MaskTexture.GetImage();
            _maskImageLoaded = true;
            if (_maskImage != null && _maskImage.IsCompressed() && _maskImage.Decompress() != Error.Ok)
                _maskImage = null;
        }

        var image = _maskImage;
        if (image == null || image.IsEmpty())
            return false;

        var bounds = MapUIControl.Instance?.MapBorders ?? new Aabb(-100, 0, -100, 200, 0, 200);
        var uv = new Vector2(
            Mathf.InverseLerp(bounds.Position.X, bounds.Position.X + bounds.Size.X, mapPosition.X),
            Mathf.InverseLerp(bounds.Position.Z, bounds.Position.Z + bounds.Size.Z, mapPosition.Y)
        );
        var pixel = image.GetPixel(
            Mathf.Clamp((int)(uv.X * image.GetWidth()), 0, image.GetWidth() - 1),
            Mathf.Clamp((int)(uv.Y * image.GetHeight()), 0, image.GetHeight() - 1)
        );
        return pixel.R >= LockedIconThreshold;
    }

    public void Unlock()
    {
        if (IsUnlocked)
            return;

        IsUnlocked = true;
        IsRevealing = true;
        var save = GlobalItemSystem.Load();
        save.GlobalFlags[GetPersistentId()] = 1;
        save.Save();

        PauseMenu.Instance?.OpenMap();
        MapUIControl.Instance?.RevealMapSection(this);
    }

    bool ReadUnlockedState()
    {
        var save = GlobalItemSystem.Load();
        return save.GlobalFlags.TryGetValue(GetPersistentId(), out var unlocked) && unlocked != 0;
    }
}