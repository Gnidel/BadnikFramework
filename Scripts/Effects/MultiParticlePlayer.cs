using System;
using Godot;

public partial class MultiParticlePlayer : Node3D
{
    private bool Activated = false;

    [Export]
    public bool DestroyAfterDone = true;

    [Export]
    public GpuParticles3D[] Particles = new GpuParticles3D[0];

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    private double particleTimer = 0;

    public override void _Process(double delta)
    {
        if (Activated)
        {
            particleTimer += delta;

            Activated = false;
            foreach (var particle in Particles)
            {
                if (particle.Emitting && particleTimer < particle.Lifetime)
                    Activated = true;
                break;
            }
            if (AudioPlayer != null && AudioPlayer.Playing)
            {
                Activated = true;
            }
            if (DestroyAfterDone && !Activated)
            {
                this.QueueFree();
            }
        }
    }

    public void Play()
    {
        if (Activated)
            return;
        Activated = true;
        particleTimer = 0;
        Emit();
    }

    void Emit()
    {
        foreach (var particle in Particles)
        {
            particle.Emitting = true;
        }
        if (AudioPlayer != null)
        {
            AudioPlayer.Play();
        }
    }
}
