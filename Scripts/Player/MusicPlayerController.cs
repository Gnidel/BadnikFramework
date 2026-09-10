using System;
using Godot;

public partial class MusicPlayerController : AudioStreamPlayer
{
    [Export]
    public AudioStream MainMusic; // Stage theme, boss theme etc.

    [Export]
    public AudioStream OverrideMusic; // Invincibility theme, jingles etc.

    public static MusicPlayerController Instance;

    public override void _EnterTree()
    {
        if (Instance != null && IsInstanceValid(Instance) && Instance.IsInsideTree())
        {
            GD.PrintErr("Multiple instances of MusicPlayerController!");
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SwitchMusicToMain()
    {
        if (this.Stream != MainMusic)
        {
            this.Stream = MainMusic;
        }
        if (!this.Playing)
        {
            this.Playing = true;
        }
        this.Autoplay = true;
    }

    public void SwitchMusicToOverride()
    {
        if (this.Stream != OverrideMusic)
        {
            this.Stream = OverrideMusic;
        }
        if (!this.Playing)
        {
            this.Playing = true;
        }
        this.Autoplay = true;
    }
}
