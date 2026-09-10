using System;
using System.Collections.Generic;
using Godot;

public partial class SpecialStageEntrance : Area3D
{
    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream CollectSoundEffect;

    [Export]
    public AudioStream WarpSoundEffect;

    [Export]
    public MeshInstance3D RingEnabledMesh;

    [Export]
    public MeshInstance3D RingDisabledMesh;

    [Export]
    public Vector3 RotationAxis = new Vector3(1, 1, 0);

    [Export]
    public float RotationSpeed = 1;
    private string NextStagePath;

    [Export]
    public ColorRect WhiteOut;

    [Export]
    public float WhiteOutSpeed = 1;

    [Export]
    public string[] ChaosEmeraldItemNames = new string[0]; // Chaos Emeralds.

    [Export]
    public string[] SpecialStagePaths = new string[0];

    public static HashSet<Vector3> CollectedSpecialStagesEntrances = new HashSet<Vector3>(); // The assumption is that a special stage on the same position is the same after reload.

    private enum StageTransitionPhase
    {
        NOT_COLLECTED,
        TRANSITION_WHITEOUT,
        WAITING_FOR_SOUND,
        DONE,
        COLLECTED_NOWARP,
    }

    private StageTransitionPhase stageTransitionPhase = StageTransitionPhase.NOT_COLLECTED;

    public void OnBodyEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (
            player != null
            && stageTransitionPhase == StageTransitionPhase.NOT_COLLECTED
            && !CollectedSpecialStagesEntrances.Contains(this.GlobalPosition)
        )
        {
            NextStagePath = GetNextStagePath(player);
            //if (NextStagePath == "")
            //{
            //	this.QueueFree();   // TODO: Add some effect
            //	player.PlayerInventory.AddItemCount("rings", 50);
            //	return;
            //}

            AudioPlayer.Stop();
            AudioPlayer.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled;
            AudioPlayer.PanningStrength = 0;
            AudioPlayer.Stream = CollectSoundEffect;
            AudioPlayer.Play();

            if (NextStagePath != "")
            {
                CheckpointSystem.CheckpointScene = GetTree().CurrentScene.SceneFilePath;
                CheckpointSystem.CheckpointPos = this.GlobalTransform;
                CheckpointSystem.CheckpointCameraPos = PlayerCamera
                    .Instances[player.PlayerID]
                    .GlobalTransform;
                CheckpointSystem.CheckpointTime = StageData.Instance.ElapsedTime;
                CheckpointSystem.SetItemsFromPlayers(new string[] { });
            }

            RingEnabledMesh.Visible = false;
            RingDisabledMesh.Visible = false;

            if (NextStagePath != "")
            {
                CollectedSpecialStagesEntrances.Add(this.GlobalPosition);
                StageData.Instance.CountTime = false;
                ResourceLoader.LoadThreadedRequest(NextStagePath);

                LoadingScreen.TrackedProgressResourcePaths = [NextStagePath];
                stageTransitionPhase = StageTransitionPhase.TRANSITION_WHITEOUT;
            }
            else
            {
                stageTransitionPhase = StageTransitionPhase.COLLECTED_NOWARP;
                player.PlayerInventory.AddItemCount("rings", 50);
            }
        }
    }

    public override void _Ready()
    {
        if (CollectedSpecialStagesEntrances.Contains(this.GlobalPosition))
        {
            RingEnabledMesh.Visible = false;
            RingDisabledMesh.Visible = true;
        }
        else
        {
            RingEnabledMesh.Visible = true;
            RingDisabledMesh.Visible = false;
        }

        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.BodyEntered += this.OnBodyEnter;
        }
    }

    public override void _Process(double delta)
    {
        switch (stageTransitionPhase)
        {
            case StageTransitionPhase.NOT_COLLECTED:
                return;
            case StageTransitionPhase.TRANSITION_WHITEOUT:
                WhiteOut.Color = new Color(1, 1, 1, 0).Lerp(
                    new Color(1, 1, 1, 1),
                    Mathf.Clamp(WhiteOut.Color.A + (float)delta * WhiteOutSpeed, 0, 1)
                );
                if (Mathf.IsEqualApprox(WhiteOut.Color.A, 1))
                {
                    stageTransitionPhase = StageTransitionPhase.WAITING_FOR_SOUND;
                    AudioPlayer.Stream = WarpSoundEffect;
                    AudioPlayer.Play(0);
                }
                LoadingScreen.InitialColor = new Color(1, 1, 1, 1);
                foreach (var player in PlayerController.Instances)
                {
                    player.GlobalPosition = player.GlobalPosition.Lerp(
                        this.GlobalPosition,
                        (float)delta * 10f
                    );
                }
                break;
            case StageTransitionPhase.WAITING_FOR_SOUND:
                if (!AudioPlayer.Playing)
                {
                    stageTransitionPhase = StageTransitionPhase.DONE;
                }
                break;
            case StageTransitionPhase.DONE:

                if (
                    ResourceLoader.LoadThreadedGetStatus(NextStagePath)
                    == ResourceLoader.ThreadLoadStatus.Loaded
                )
                {
                    var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(NextStagePath);
                    GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
                }
                break;
            case StageTransitionPhase.COLLECTED_NOWARP:
                if (!AudioPlayer.Playing)
                {
                    this.QueueFree();
                }

                break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        RingEnabledMesh.Rotate(RotationAxis.Normalized(), RotationSpeed * (float)delta);
        RingDisabledMesh.Rotate(RotationAxis.Normalized(), RotationSpeed * (float)delta);
    }

    private string GetNextStagePath(PlayerController player)
    {
        var inventory = player.PlayerInventory;
        int CurrentSpecialStageIdx = 0;
        foreach (var itemName in ChaosEmeraldItemNames)
        {
            if (inventory.GetItemCount(itemName) > 0)
            {
                CurrentSpecialStageIdx++;
            }
            else
            {
                break;
            }
        }
        if (SpecialStagePaths.Length <= CurrentSpecialStageIdx)
            return "";
        return SpecialStagePaths[CurrentSpecialStageIdx];
    }
}
