using System;
using System.Collections.Generic;
using Godot;

public partial class FinishScreen : Control
{
    [Export]
    public Label MessageLabel;

    [Export]
    public Label TimeLabel;
    private double DisplayTime;

    [Export]
    public Label RingsLabel;
    private double DisplayRings;

    [Export]
    public Label SpheresText;

    [Export]
    public Label SpheresLabel;
    private double DisplaySpheres;

    [Export]
    public Label DeathsText;

    [Export]
    public Label DeathsLabel;
    private double DisplayDeaths;

    [Export]
    public Label RankLabel;
    private string DisplayRank = "";

    [Export]
    public string[] StageItems = new string[0];

    [Export]
    public string[] GlobalItems = new string[0];

    [Export]
    public ProgressBar ProgressBarS;

    [Export]
    public ProgressBar ProgressBarA;

    [Export]
    public ProgressBar ProgressBarB;

    [Export]
    public ProgressBar ProgressBarC;

    [Export]
    public AudioStreamPlayer2D AudioEffectPlayer;

    [Export]
    public AudioStream CountdownSound;

    [Export]
    public AudioStream SRankSound;

    [Export]
    public AudioStream BadRankSound;

    [Export]
    public AudioStream ERankSound;

    [Export]
    public AudioStream SRankMusic;

    [Export]
    public AudioStream BadRankMusic;

    [Export]
    public AudioStream ERankMusic;

    [Export]
    public double MinCountdownTime = 6f;
    private double countdownTimer = 0f;

    private bool finishedCountdown;

    public static string NextStagePath;
    public static string[] StagesToUnlock = new string[0];

    public override void _EnterTree()
    {
        this.ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _Input(InputEvent @event)
    {
        try
        {
            if (GetTree().Paused)
                return;

            if (
                !finishedCountdown
                && this.ProcessMode != ProcessModeEnum.Disabled
                && (Input.IsActionPressed("ui_accept") || Input.IsActionPressed("any_actionjump"))
            )
            {
                var elapsedTime = StageData.Instance.ElapsedTime;

                DisplayTime = elapsedTime;
                int rings = GetAllPlayersRings();
                DisplayRings = rings;
                DisplaySpheres = GetAllPlayersBlueSpheres();
                DisplayDeaths = CheckpointSystem.DeathCount;
                finishedCountdown = true;
                AudioEffectPlayer.Stop();
                UpdateLabels();
                PlayRankSound();

                switch (DisplayRank)
                {
                    case "S":
                        AudioEffectPlayer.Stream = SRankSound;
                        break;
                    case "E":
                        AudioEffectPlayer.Stream = ERankSound;
                        break;
                    default:
                        AudioEffectPlayer.Stream = BadRankSound;
                        break;
                }
                AudioEffectPlayer.Play();
            }
            else if (
                finishedCountdown
                && this.ProcessMode != ProcessModeEnum.Disabled
                && (Input.IsActionPressed("ui_accept") || Input.IsActionPressed("any_actionjump"))
                && !LoadingScreen.Instance.Returning
            )
            {
                LoadingScreen.Instance.Return();
                SaveResults();
                if (
                    ResourceLoader.LoadThreadedGetStatus(NextStagePath)
                    == ResourceLoader.ThreadLoadStatus.InvalidResource
                )
                {
                    ResourceLoader.LoadThreadedRequest(NextStagePath);
                }
            }
            else if (
                finishedCountdown
                && this.ProcessMode != ProcessModeEnum.Disabled
                && Input.IsActionPressed("ui_select")
                && !LoadingScreen.Instance.Returning
            )
            {
                LoadingScreen.Instance.ReloadSceneAfterTransition();
            }
        }
        catch (Exception e)
        {
            ErrorLog.AddMessage(e.Message + e.StackTrace.ToString());
        }
    }

    void SaveResults()
    {
        var save = SaveData.Load();
        var levelSave = save.LevelSaves.GetValueOrDefault(
            StageData.Instance.StageUniqueIdentifier,
            new SaveData.LevelSave()
        );

        var characterName = "Sonic";
        // Time
        var prevBestTime = levelSave.BestTime.GetValueOrDefault(characterName, Mathf.Inf);
        levelSave.BestTime[characterName] = Mathf.Min(StageData.Instance.ElapsedTime, prevBestTime);

        // Rank
        var bestPrevRank = levelSave.BestRank.GetValueOrDefault(characterName, "F");
        if (bestPrevRank == "S" || DisplayRank == "S")
        {
            levelSave.BestRank[characterName] = "S";
        }
        else if (String.Compare(bestPrevRank, DisplayRank, true) >= 0)
        {
            levelSave.BestRank[characterName] = DisplayRank;
        }
        else
        {
            levelSave.BestRank[characterName] = bestPrevRank;
        }

        // Items

        foreach (var item in StageItems)
        {
            int itemCount = 0;
            for (int p = 0; p < PlayerController.Instances.Count; p++)
            {
                itemCount += PlayerController.Instances[p].PlayerInventory.GetItemCount(item);
            }
            levelSave.Items[item] = itemCount;
        }

        if (StageData.Instance.Style != StageData.StageStyle.SpecialStage)
        {
            foreach (var item in GlobalItems)
            {
                int itemCount = 0;
                for (int p = 0; p < PlayerController.Instances.Count; p++)
                {
                    itemCount += PlayerController.Instances[p].PlayerInventory.GetItemCount(item);
                }

                save.GlobalItems[item] = itemCount;
            }
        }

        // Save
        save.LevelSaves[StageData.Instance.StageUniqueIdentifier] = levelSave;

        // Unlock new stages
        foreach (var stage in StagesToUnlock)
        {
            save.UnlockLevel(stage);
        }

        save.Save();
    }

    bool firstFrame = true;

    public override void _Process(double delta)
    {
        try
        {
            if (firstFrame)
            {
                if (!finishedCountdown && this.ProcessMode != ProcessModeEnum.Disabled)
                {
                    var predictedRank = CalculateRank(
                        StageData.Instance.ElapsedTime,
                        GetAllPlayersRings(),
                        GetAllPlayersBlueSpheres(),
                        CheckpointSystem.DeathCount
                    );
                    SetAnimation(predictedRank, true);
                }
                firstFrame = false;
            }

            //foreach (var cam in PlayerCamera.Instances)
            //{
            //    if (cam.Value.Player.PlayerSkinController.HeadCameraFocusPoint != null)
            //    {
            //        cam.Value.LookAt(cam.Value.Player.PlayerSkinController.HeadCameraFocusPoint.GlobalPosition);
            //        cam.Value.Rotate(Vector3.Up, -Mathf.DegToRad(15));
            //    }
            //}

            if (finishedCountdown)
            {
                if (
                    ResourceLoader.LoadThreadedGetStatus(NextStagePath)
                        == ResourceLoader.ThreadLoadStatus.Loaded
                    && LoadingScreen.Instance.Returning
                    && LoadingScreen.Instance.TransitionProcess >= 1
                )
                {
                    if (StageData.Instance.Style != StageData.StageStyle.SpecialStage)
                    {
                        CheckpointSystem.Reset();
                    }
                    var nextScene = CheckpointSystem.CheckpointScene;

                    if (NextStagePath == "res://" || NextStagePath == "" || NextStagePath == null)
                    {
                        nextScene = ProjectSettings
                            .GetSetting("application/run/main_scene")
                            .AsString();
                        //GD.PrintErr("Null next scene. Loading {", nextScene, "} as fallback");
                        //var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(nextScene);
                        GetTree().ChangeSceneToFile(nextScene);
                    }
                    else
                    {
                        var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(NextStagePath);
                        GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
                    }
                }
                return;
            }

            SetCharacterNames();
            if (StageData.Instance.Style == StageData.StageStyle.SpecialStage)
            {
                DeathsText.Visible = false;
                DeathsLabel.Visible = false;
            }
            else
            {
                SpheresText.Visible = false;
                SpheresLabel.Visible = false;
            }

            countdownTimer += delta;
            if (!AudioEffectPlayer.Playing)
            {
                AudioEffectPlayer.Stream = CountdownSound;
                AudioEffectPlayer.Play();
            }

            // Time countdown
            var elapsedTime = StageData.Instance.ElapsedTime;
            if (DisplayTime < elapsedTime)
            {
                DisplayTime += elapsedTime / 3f * delta;
            }
            if (DisplayTime >= elapsedTime)
            {
                DisplayTime = elapsedTime;
            }
            else
            {
                UpdateLabels();
                return;
            }

            // Rings countdown
            int rings = GetAllPlayersRings();
            if (DisplayRings < rings)
            {
                DisplayRings += (rings / 3f * delta);
            }
            if (DisplayRings >= rings)
            {
                DisplayRings = rings;
            }
            else
            {
                UpdateLabels();
                return;
            }

            int spheres = GetAllPlayersBlueSpheres();
            if (DisplaySpheres < spheres)
            {
                DisplaySpheres += (spheres / 3f * delta);
            }
            if (DisplaySpheres >= spheres)
            {
                DisplaySpheres = spheres;
            }
            else
            {
                UpdateLabels();
                return;
            }

            // Deaths countdown
            if (DisplayDeaths < CheckpointSystem.DeathCount)
            {
                DisplayDeaths += CheckpointSystem.DeathCount / 3f * delta;
            }
            if (DisplayDeaths >= CheckpointSystem.DeathCount)
            {
                DisplayDeaths = CheckpointSystem.DeathCount;
            }
            else
            {
                UpdateLabels();
                return;
            }
            AudioEffectPlayer.Stop();
            if (countdownTimer < MinCountdownTime)
            {
                UpdateLabels();
                return;
            }

            finishedCountdown = true;
            UpdateLabels();
            PlayRankSound();
        }
        catch (Exception e)
        {
            ErrorLog.AddMessage(e.Message + e.StackTrace.ToString());
        }
    }

    void UpdateLabels()
    {
        TimeLabel.Text = TimeSpan.FromSeconds(DisplayTime).ToString(@"mm\:ss\:ff");

        RingsLabel.Text = Mathf.CeilToInt(DisplayRings).ToString();
        SpheresLabel.Text = Mathf.CeilToInt(DisplaySpheres).ToString();
        DeathsLabel.Text = Mathf.CeilToInt(DisplayDeaths).ToString();
        DisplayRank = CalculateRank(DisplayTime, DisplayRings, DisplaySpheres, DisplayDeaths);

        if (finishedCountdown)
        {
            RankLabel.Text = DisplayRank;
            SetAnimation(DisplayRank, false);
        }
        else
        {
            RankLabel.Text = "";
        }
    }

    string CalculateRank(double time, double rings, double blueSpheres, double deaths)
    {
        double timeBonus = StageData.Instance.SRankTime / time * StageData.Instance.RankWeightTime;
        double ringsBonus =
            (double)rings
            / (double)StageData.Instance.SRankRings
            * StageData.Instance.RankWeightRings;
        double spheresBonus = 0;
        if (StageData.Instance.RankWeightSpheres > 0 && StageData.Instance.SRankBlueSpheres > 0)
        {
            spheresBonus =
                (double)blueSpheres
                / (double)StageData.Instance.SRankBlueSpheres
                * StageData.Instance.RankWeightSpheres;
        }
        double deathsBonus = deaths * StageData.Instance.RankWeightDeath;
        double totalBonus = timeBonus + ringsBonus + spheresBonus - deathsBonus;

        const double SRankScore = 1f;
        const double ARankScore = 0.8f;
        const double BRankScore = 0.6f;
        const double CRankScore = 0.4f;
        const double DRankScore = 0.2f;

        ProgressBarS.Value = (totalBonus - ARankScore) / (SRankScore - ARankScore) * 100f;
        ProgressBarA.Value = (totalBonus - BRankScore) / (ARankScore - BRankScore) * 100f;
        ProgressBarB.Value = (totalBonus - CRankScore) / (BRankScore - CRankScore) * 100f;
        ProgressBarC.Value = (totalBonus - DRankScore) / (CRankScore - DRankScore) * 100f;

        string rank = "E";
        if (totalBonus >= SRankScore)
        {
            rank = "S";
        }
        else if (totalBonus >= ARankScore)
        {
            rank = "A";
        }
        else if (totalBonus >= BRankScore)
        {
            rank = "B";
        }
        else if (totalBonus >= CRankScore)
        {
            rank = "C";
        }
        else if (totalBonus >= DRankScore)
        {
            rank = "D";
        }
        return rank;
        //DisplayRank = /*((int)(totalBonus * 100f)).ToString() + "%: " +*/ rank;
    }

    int GetAllPlayersRings()
    {
        int rings = 0;
        foreach (var player in PlayerController.Instances)
        {
            rings += player.PlayerInventory.GetItemCount("rings");
        }
        return rings;
    }

    int GetAllPlayersBlueSpheres()
    {
        int spheres = 0;
        foreach (var player in PlayerController.Instances)
        {
            spheres += player.PlayerInventory.GetItemCount("bluesphere");
        }
        return spheres;
    }

    void PlayRankSound()
    {
        MusicPlayerController.Instance.Stop();
        AudioEffectPlayer.Stop();
        switch (DisplayRank)
        {
            case "S":
                AudioEffectPlayer.Stream = SRankSound;
                MusicPlayerController.Instance.Stream = SRankMusic;
                break;
            case "E":
                AudioEffectPlayer.Stream = ERankSound;
                MusicPlayerController.Instance.Stream = ERankMusic;
                break;
            default:
                AudioEffectPlayer.Stream = BadRankSound;
                MusicPlayerController.Instance.Stream = BadRankMusic;
                break;
        }
        AudioEffectPlayer.Play();
        MusicPlayerController.Instance.Play();
    }

    void SetAnimation(string rank, bool isSkip)
    {
        var animationName = "Rank " + rank;

        foreach (var player in PlayerController.Instances)
        {
            if (player.PlayerSkinController.SkipRankWaitAnimation == isSkip)
            {
                try
                {
                    player.PlayerSkinController.TravelAnimation(animationName);
                }
                catch (Exception e) { } // Ignore lack of animations
            }
        }
    }

    void SetCharacterNames()
    {
        string charactersNames = "";
        foreach (var player in PlayerController.Instances)
        {
            if (charactersNames != "")
            {
                charactersNames += " & ";
            }
            charactersNames += player.CharacterName;
        }
        MessageLabel.Text = charactersNames + "\ngot through\n" + StageData.Instance.StageActName;
    }
}
