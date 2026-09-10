using System;
using Godot;

public partial class SpecialStageManiaManager : Node
{
    [Export]
    public int Mach = 0;

    [Export]
    public float[] MachSpeeds = new float[3] { 50, 100, 150 };
    public float MachProgress;

    [Export]
    public AudioStream[] MachMusics = new AudioStream[3];

    [Export]
    public Control SpecialStageUI;

    [Export]
    public RichTextLabel MachLabel;

    [Export]
    public ProgressBar MachProgressBar;

    [Export]
    public float ProgressPerBlueSphere = 0.01f;

    [Export]
    public AudioStreamPlayer MachSpeedSoundPlayer;

    [Export]
    public string VictoryItem = "chaosemerald_red";

    [Export]
    public GoalRing GoalRing;

    [Export]
    public PathFollow3D EmeraldPathFollow;

    [Export]
    public float EmeraldCurrentSpeed = 55;

    [Export]
    public float EmeraldSpeed = 100;

    [Export]
    public Node3D HomingTarget;

    [Export]
    public float EnableHomingAfterTime = 10f;
    private float homingTimer = 0;

    public override void _Ready()
    {
        MusicPlayerController.Instance.MainMusic = MachMusics[Mach];
        MusicPlayerController.Instance.SwitchMusicToMain();

        foreach (var player in PlayerController.Instances)
        {
            player.OnlyFastRotation = true;
            player.AntiInertiaFactor = 1;
            player.DisableNoInputDecceleration = true;
            player.VelocityXZ = -player.Basis.Z * 10f;
            player.Acceleration = new Curve();
            player.Acceleration.AddPoint(new Vector2(0, 0));

            ActionDash actionDash = player.GetNodeOrNull<ActionDash>(
                "./PlayerControl/Actions/ActionDash"
            );
            if (actionDash != null)
            {
                actionDash.ProcessMode = ProcessModeEnum.Disabled;
            }

            ActionRoll actionRoll = player.GetNodeOrNull<ActionRoll>(
                "./PlayerControl/Actions/ActionRoll"
            );
            if (actionRoll != null)
            {
                actionRoll.CanManuallyRoll = false;
                actionRoll.CanSpinDash = false;
            }

            ActionBoost actionBoost = player.GetNodeOrNull<ActionBoost>(
                "./PlayerControl/Actions/ActionBoost"
            );
            if (actionBoost != null)
            {
                actionBoost.ProcessMode = ProcessModeEnum.Disabled;
            }

            ActionDrift actionDrift = player.GetNodeOrNull<ActionDrift>(
                "./PlayerControl/Actions/ActionDrift"
            );
            if (actionDrift != null)
            {
                actionDrift.ProcessMode = ProcessModeEnum.Disabled;
            }

            ActionQuickStep actionQuickStep = player.GetNodeOrNull<ActionQuickStep>(
                "./PlayerControl/Actions/ActionQuickStep"
            );
            if (actionQuickStep != null)
            {
                actionQuickStep.ProcessMode = ProcessModeEnum.Disabled;
            }

            ActionGun actionGun = player.GetNodeOrNull<ActionGun>(
                "./PlayerControl/Actions/ActionGun"
            );
            if (actionGun != null)
            {
                actionGun.ProcessMode = ProcessModeEnum.Disabled;
            }

            ActionHoming actionHoming = player.GetNodeOrNull<ActionHoming>(
                "./PlayerControl/Actions/ActionHoming"
            );
            if (actionHoming != null)
            {
                actionHoming.ProcessMode = ProcessModeEnum.Inherit;
            }

            player.PlayerInventory.SetItemCount("bluespherebonus", 1);

            //PlayerCamera.Instances[player.PlayerID].CameraTargets.Add(EmeraldPathFollow);
        }

        GoalRing.Visible = false;
        GoalRing.ProcessMode = ProcessModeEnum.Disabled;
        GoalRing.NextLevel = CheckpointSystem.CheckpointScene;
        HomingTarget.Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (StageData.Instance.IsFinished)
        {
            return;
        }

        if (PlayerDamage.AlivePlayersCount() == 0)
        {
            if (
                ResourceLoader.LoadThreadedGetStatus(CheckpointSystem.CheckpointScene)
                == ResourceLoader.ThreadLoadStatus.InvalidResource
            )
            {
                ResourceLoader.LoadThreadedRequest(CheckpointSystem.CheckpointScene);
                LoadingScreen.Instance.Return();
                LoadingScreen.Instance.Visible = true;
                LoadingScreen.Instance.SetTexts("Loading", "");
            }
            if (
                ResourceLoader.LoadThreadedGetStatus(CheckpointSystem.CheckpointScene)
                    == ResourceLoader.ThreadLoadStatus.Loaded
                && LoadingScreen.Instance.TransitionProcess >= 1
            )
            {
                var nextScene = CheckpointSystem.CheckpointScene;
                var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(nextScene);
                GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
            }
        }

        EmeraldTravel(delta);

        homingTimer += (float)delta;
        if (homingTimer > EnableHomingAfterTime)
        {
            HomingTarget.Visible = true;
        }

        foreach (var player in PlayerController.Instances)
        {
            if (player.PlayerDamage.IsDead)
                continue;
            if (player.VelocityXZ.IsZeroApprox())
                player.VelocityXZ = -player.Basis.Z;
            player.VelocityXZ = player.VelocityXZ.Lerp(
                player.VelocityXZ.Normalized() * MachSpeeds[Mach],
                (float)delta
            );

            var blueSpheres = player.PlayerInventory.GetItemCount("bluesphere");
            var blueSpheresBonus = player.PlayerInventory.GetItemCount("bluespherebonus");
            player.PlayerInventory.SetItemCount("bluesphere", 0);
            MachProgress += blueSpheres * ProgressPerBlueSphere;
            player.PlayerInventory.AddItemCount("stolenbluesphere", blueSpheres * blueSpheresBonus);

            var rings = player.PlayerInventory.GetItemCount("rings");
            StageData.Instance.MaxTime += rings;
            player.PlayerInventory.SetItemCount("rings", 0);
            player.PlayerInventory.AddItemCount("stolenrings", rings);

            //player.PlayerInventory.SetItemCount("specialstage_mach", Mach+1);

            var emerald = player.PlayerInventory.GetItemCount(VictoryItem);
            if (emerald > 0)
            {
                Victory(player);
            }
        }

        if (MachProgress >= 1)
        {
            MachSpeedSoundPlayer.Play(0);
            MachProgress -= 1;
            if (Mach < 2)
            {
                Mach++;
                MusicPlayerController.Instance.MainMusic = MachMusics[Mach];
                MusicPlayerController.Instance.SwitchMusicToMain();
            }
            else
            {
                foreach (var player in PlayerController.Instances)
                {
                    player.PlayerInventory.AddItemCount("bluespherebonus", 1);
                }
            }
        }

        //MachProgress += (float)delta / 20f;

        MachLabel.Text = "[center]Mach " + (Mach + 1).ToString() + "[/center]";
        MachProgressBar.Value = MachProgress * 100;
    }

    void Victory(PlayerController winner)
    {
        var save = SaveData.Load();
        save.GlobalItems[VictoryItem] = 1;
        save.Save();

        GoalRing.Visible = false;
        GoalRing.ProcessMode = ProcessModeEnum.Inherit;
        GoalRing.OnCollissionEnter(winner);
        SpecialStageUI.Visible = false;
        foreach (var player in PlayerController.Instances)
        {
            player.PlayerInventory.AddItemCount(
                "rings",
                player.PlayerInventory.GetItemCount("stolenrings")
            );
            player.PlayerInventory.AddItemCount(
                "bluesphere",
                player.PlayerInventory.GetItemCount("stolenbluesphere")
            );
            foreach (var p in PlayerController.Instances)
            {
                CheckpointSystem.CheckpointItems[p.PlayerID][VictoryItem] = 1;
            }
        }
    }

    void EmeraldTravel(double delta)
    {
        EmeraldCurrentSpeed = Mathf.Lerp(EmeraldCurrentSpeed, EmeraldSpeed, (float)delta / 60f);
        //var path = EmeraldPathFollow.GetNode<Path3D>("..");

        //foreach (var player in PlayerController.Instances)
        //{
        //var playerProgress = path.Curve.GetClosestOffset(path.GlobalTransform.AffineInverse() * player.GlobalPosition);
        //if (playerProgress > EmeraldPathFollow.Progress)
        {
            EmeraldPathFollow.Progress += EmeraldCurrentSpeed * (float)delta;
        }
        //else
        //         {
        //             EmeraldPathFollow.Progress += EmeraldSpeed * (float)delta;
        //         }
        //}
    }
}
