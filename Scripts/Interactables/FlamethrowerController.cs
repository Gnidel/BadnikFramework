using System;
using Godot;

public partial class FlamethrowerController : Node3D
{
    [Export]
    public GpuParticles3D Particles;

    [Export]
    public Hitbox Hitbox;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public float HitboxActivationDelay = 0.5f;

    [Export]
    public bool IsActive = true;
    private float hitboxActivationTimer = 0;

    public override void _Ready()
    {
        if (IsActive)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }

    public override void _Process(double delta)
    {
        if (IsActive)
        {
            if (hitboxActivationTimer > HitboxActivationDelay)
            {
                if (!Hitbox.Monitorable)
                {
                    Hitbox.Monitorable = true;
                }
            }
            else
            {
                hitboxActivationTimer += (float)delta;
            }
        }
    }

    public void Switch()
    {
        IsActive = !IsActive;
        if (IsActive)
        {
            Activate();
        }
        else
        {
            Deactivate();
        }
    }

    public void Activate()
    {
        Particles.Emitting = true;
        hitboxActivationTimer = 0;
        AudioPlayer.Playing = true;
    }

    public void Deactivate()
    {
        Particles.Emitting = false;
        Hitbox.Monitorable = false;
        AudioPlayer.Playing = false;
    }
}
