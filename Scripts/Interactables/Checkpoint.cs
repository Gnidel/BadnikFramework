using System;
using System.Security.Cryptography.X509Certificates;
using Godot;

public partial class Checkpoint : Node3D
{
    [Export]
    public AudioStreamPlayer3D AudioSource;

    [Export]
    public Node3D SpawnPos;

    [Export]
    public Node3D CameraSpawnPos;

    [Export]
    public Node3D UpCheckpointSignpost;

    [Export]
    public Node3D LeftCheckpointSignpost;

    [Export]
    public Node3D RightCheckpointSignpost;

    [Export]
    public MeshInstance3D[] MeshWithBalls = new MeshInstance3D[0];

    [Export]
    public Node3D Ribbon;

    private bool wasActivated = false;
    private ulong spawnTime;

    public override void _Ready()
    {
        spawnTime = Time.GetTicksMsec();
        foreach (var mesh in MeshWithBalls)
        {
            mesh.GetActiveMaterial(2).Set("albedo_color", new Color(0, 0, 1));
            mesh.GetActiveMaterial(2).Set("emission", new Color(0, 0, 1));
        }
    }

    public void OnBodyEnter(Node3D other)
    {
        if (wasActivated)
            return;

        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            CheckpointSystem.CheckpointPos = SpawnPos.GlobalTransform;
            CheckpointSystem.CheckpointCameraPos = CameraSpawnPos.GlobalTransform;
            CheckpointSystem.CheckpointTime = StageData.Instance.ElapsedTime;
            CheckpointSystem.CheckpointScene = GetTree().CurrentScene.SceneFilePath;
            CheckpointSystem.SetItemsFromPlayers(new string[] { "rings" });

            if (Time.GetTicksMsec() - spawnTime > 5000) // If player gets respawned from checkpoint, we don't want sound
            {
                AudioSource.Play();
            }
            wasActivated = true;

            foreach (
                var checkpoint in new[]
                {
                    UpCheckpointSignpost,
                    LeftCheckpointSignpost,
                    RightCheckpointSignpost,
                }
            )
            {
                if (checkpoint == null)
                    continue;
                var animationPlayer = checkpoint.GetNodeOrNull<AnimationPlayer>(
                    "./AnimationPlayer"
                );
                if (animationPlayer != null)
                {
                    animationPlayer.Play("triggered");
                }
            }
            foreach (var mesh in MeshWithBalls)
            {
                mesh.GetActiveMaterial(2).Set("albedo_color", new Color(1, 0, 0));
                mesh.GetActiveMaterial(2).Set("emission", new Color(1, 0, 0));
            }

            if (Ribbon != null)
            {
                Ribbon.Visible = false;
            }
        }
    }
}
