using System;
using Godot;

public partial class MusicTrigger : Area3D
{
    [Export]
    public AudioStream Music;

    [Export]
    public bool SwitchMainMusic = true;

    [Export]
    public bool SwitchOverrideMusic = false;

    public void OnBodyEntered(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null && !player.NpcPartnerControl.IsNpc)
        {
            ChangeMusic();
        }
    }

    public void ChangeMusic()
    {
        if (MusicPlayerController.Instance != null)
        {
            if (SwitchMainMusic)
            {
                if (MusicPlayerController.Instance.MainMusic != Music)
                {
                    MusicPlayerController.Instance.MainMusic = Music;
                }
                MusicPlayerController.Instance.SwitchMusicToMain();
            }
            else if (SwitchOverrideMusic)
            {
                if (MusicPlayerController.Instance.OverrideMusic != Music)
                {
                    MusicPlayerController.Instance.OverrideMusic = Music;
                }
                MusicPlayerController.Instance.SwitchMusicToOverride();
            }
        }
    }
}
