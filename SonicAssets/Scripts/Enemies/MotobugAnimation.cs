using System;
using Godot;

public partial class MotobugAnimation : Node
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public AnimationPlayer AnimationPlayer;

    [Export]
    public RigidBody3D RigidBody;

    [Export]
    public MeshInstance3D Mesh;

    [Export]
    public int TireSurfaceIdx;

    [Export]
    public GpuParticles3D[] SpeedParticles = new GpuParticles3D[0];

    [Export]
    public AudioStreamPlayer3D AggroAudioPlayer;

    [Export]
    public string IdleAnimation = "mtr_idle";

    [Export]
    public string MoveAnimation = "mtr_move";

    [Export]
    public string MoveRush = "mtr_rush";

    private bool AlreadyAccelerated = false;

    public override void _PhysicsProcess(double delta)
    {
        if (EnemyControl.HP <= 0)
            return;
        // Set animation
        var speed = Vector3Utils.LengthAlongsideVector(
            RigidBody.LinearVelocity,
            -RigidBody.Basis.Z
        );
        if (speed < 1)
        {
            AnimationPlayer.Play(IdleAnimation);
        }
        else if (speed >= 30)
        {
            AnimationPlayer.Play(MoveRush);
        }
        else
        {
            AnimationPlayer.Play(MoveAnimation);
        }

        // UV scroll on tire
        if (Mesh != null)
        {
            var material = Mesh.Mesh.SurfaceGetMaterial(TireSurfaceIdx);
            var uvOffset = material.Get("uv1_offset").AsVector3();
            var ScrollValue = new Vector3(-speed, 0, 0);
            var newUvOffset = uvOffset + ScrollValue * (float)delta;
            material.Set("uv1_offset", newUvOffset);
        }

        // Speed particles
        foreach (var particle in SpeedParticles)
        {
            particle.Emitting = speed > 5;
        }

        // Sound
        if (speed <= 5)
        {
            AlreadyAccelerated = false;
        }
        if (speed > 5 && !AggroAudioPlayer.Playing && !AlreadyAccelerated)
        {
            AggroAudioPlayer.Play();
            AlreadyAccelerated = true;
        }
    }
}
