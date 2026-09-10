using System;
using System.Threading.Tasks;
using Godot;

public partial class GoalRing : Node3D
{
    [Export]
    public string NextLevel;

    [Export]
    public bool S3KTransition;

    [Export]
    public string[] StagesToUnlock = new string[0];

    [Export]
    public AudioStream VictoryMusic;

    [Export]
    public string Animation = "Rank Wait";

    [Export]
    public Node3D Model;

    [Export]
    public Node3D[] PlayerLockPosition = new Node3D[0];

    [Export]
    public Vector3 RotationAxis = Vector3.Up;

    [Export]
    public float RotationSpeed = 1;

    [Export]
    public ColorRect WhiteOut;

    [Export]
    public float WhiteOutTime;

    [Export]
    public Node3D CameraPosition;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream CollectSoundEffect;

    [Export] public bool DisableSuper = true;

    //[Export] public string[] TransferableItems = new string[0];

    private bool collected = false;
    private float scale = 1;
    private float collectedTime = 0;
    private bool playerLocked = false;

    public override void _Ready()
    {
        WhiteOut.Color = new Color(1, 1, 1, 0);
    }

    public override void _Process(double delta)
    {
        Model.Rotate(RotationAxis, RotationSpeed * (float)delta);
        if (collected)
        {
            scale -= (float)delta;
            if (scale <= 0)
                scale = 0;
            Model.Scale = Vector3.One * scale;

            collectedTime += (float)delta;
            if (collectedTime < WhiteOutTime)
            {
                var newColor = new Color(1, 1, 1, collectedTime / WhiteOutTime);
                WhiteOut.Color = newColor;
            }
            else
            {
                var newColor = new Color(
                    1,
                    1,
                    1,
                    Mathf.Clamp(1 - (collectedTime - WhiteOutTime * 2) / WhiteOutTime, 0, 1)
                );
                WhiteOut.Color = newColor;
                if (!playerLocked)
                {
                    playerLocked = true;
                    LockPlayers();
                    LockCameras();
                }
            }
            if (collectedTime > WhiteOutTime * 2)
            {
                PlayerUI.Instance.SetFinishMode(true);
                LoadingScreen.Instance.ColorRect.Color = new Color(0, 0, 0, 1);
                LoadingScreen.InitialColor = new Color(0, 0, 0, 1);
                foreach (var player in PlayerController.Instances)
                {
                    player.PlayerInput.TwodimensionalMode = false;
                    var splineMovement = player.GetNodeOrNull<SplineMovement>(
                        "./PlayerControl/SplineMovement"
                    );
                    if (splineMovement != null)
                    {
                        splineMovement.Path = null;
                    }
                }
            }
            if (NextLevel == null || NextLevel == "" || NextLevel == "res://")
            {
                NextLevel = ProjectSettings.GetSetting("application/run/main_scene").AsString();
            }
            FinishScreen.NextStagePath = NextLevel;
            FinishScreen.StagesToUnlock = StagesToUnlock;

            if (!S3KTransition && StageData.Instance.Style != StageData.StageStyle.SpecialStage)
            {
                CheckpointSystem.CheckpointCameraPos = null;
                CheckpointSystem.CheckpointPos = null;
                LoadingScreen.InitialColor = new Color(0, 0, 0, 1); // Black
            }
            else if (S3KTransition)
            {
                CheckpointSystem.CheckpointCameraPos = CameraPosition.GlobalTransform;
                CheckpointSystem.CheckpointPos = this.GlobalTransform;
                LoadingScreen.InitialColor = new Color(0, 0, 0, 0); // Transparent
                LoadingScreen.Instance.ColorRect.Visible = false;
            }
            LoadingScreen.TrackedProgressResourcePaths = [NextLevel];
        }
    }

    public void OnCollissionEnter(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null && !player.NpcPartnerControl.IsNpc && !collected)
        {
            collected = true;
            StageData.Instance.CountTime = false;
            StageData.Instance.IsFinished = true;
            AudioPlayer.Stop();
            AudioPlayer.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled;
            AudioPlayer.PanningStrength = 0;
            AudioPlayer.Stream = CollectSoundEffect;
            AudioPlayer.Play();

            if (DisableSuper)
            {
                foreach (var p in PlayerController.Instances)
                {
                    var actionSuper = player.GetNodeOrNull<ActionSuperTransformation>(
                        new NodePath("./PlayerControl/Actions/ActionSuperTransformation")
                    );
                    if (actionSuper != null && actionSuper.IsSuper)
                    {
                        actionSuper.StopSuper();
                    }
                }
            }
        }

        
    }

    private void LockPlayers()
    {
        MusicPlayerController.Instance.MainMusic = VictoryMusic;
        MusicPlayerController.Instance.SwitchMusicToMain();

        int playerIdx = 0;
        foreach (var player in PlayerController.Instances)
        {
            var actionAutoGimmick = player.GetNodeOrNull<ActionAutoGimmick>(
                "./PlayerControl/Actions/ActionAutoGimmick"
            );
            if (actionAutoGimmick != null)
            {
                //currentPlayer = actionAutoGimmick;
                actionAutoGimmick.StartAutoGimmick(Animation, PlayerLockPosition[playerIdx]);
                player.Grounded = true;

                //active = true;
                //AudioPlayer.Play();
            }
            player.LinearVelocity = Vector3.Zero;
            playerIdx++;
        }
    }

    private void LockCameras()
    {
        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.SetProcess(false);
            cam.Value.SetPhysicsProcess(false);
            cam.Value.GlobalTransform = CameraPosition.GlobalTransform;
        }
    }
}
