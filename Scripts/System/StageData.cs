using System;
using Godot;

// TODO: This doubles down as stage data and optimizer. It could be split into two.

public partial class StageData : Node
{
    [Export]
    public string StageZoneName; // For title card

    [Export]
    public string StageActName; // For title card

    [Export]
    public StageStyle Style;

    [Export]
    public string StageUniqueIdentifier; // How the stage will be identified in save file

    [Export]
    public AudioStream StageMusic;

    [Export]
    public bool HasRedRings = false;

    public double ElapsedTime { get; set; }

    [Export]
    public double MaxTime = -1; // If MaxTime <=0, then it's infinite

    [Export]
    public bool CountTime = true;
    public bool IsFinished = false;

    // Ranks
    [Export]
    public double SRankTime = 120f;

    [Export]
    public int SRankRings = 100;

    [Export]
    public int SRankBlueSpheres = 0;

    [Export]
    public double RankWeightTime = 0.8f;

    [Export]
    public double RankWeightRings = 0.2f;

    [Export]
    public double RankWeightSpheres = 0.0f;

    [Export]
    public double RankWeightDeath = 0.2f;

    // Culling
    [Export]
    public float EnemyCullDistance = 200;

    [Export]
    public bool EnemyCulledVisibility = false;

    [Export]
    public float ItemCullDistance = 100;

    [Export]
    public bool ItemCulledVisibility = false;
    const int ItemDistancesCheckedAtOnce = 50;
    private int ItemDistancesCheckStartFrom = 0;

    [Export]
    public Godot.Collections.Dictionary<string, Texture2D> ExtraTrackedItems = new(); // <ID/Icon>

    [Export]
    public string ExitSceneOverride = ""; // The scene you go after selecting exit. It needs to be overriden in hubs to avoid looping back to the same scene.

    public enum StageStyle
    {
        ActionStage,
        HubStage,
        SpecialStage,
    }

    public static StageData Instance;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

    public override void _Ready()
    {
        MusicPlayerController.Instance.MainMusic = StageMusic;
        MusicPlayerController.Instance.SwitchMusicToMain();
        ItemDistanceCulling(true);
        if (Style == StageStyle.HubStage)
        {
            PauseMenu.ExitScene = GetTree().CurrentScene.SceneFilePath;
        }
    }

    public override void _Process(double delta)
    {
        if (CountTime)
        {
            ElapsedTime += delta;
        }
        if (MaxTime > 0)
        {
            if (MaxTime - ElapsedTime < 0)
            {
                foreach (var player in PlayerController.Instances)
                {
                    if (Style == StageStyle.ActionStage)
                    {
                        CheckpointSystem.Reset();
                    }

                    var playerDamage = player.GetNode<PlayerDamage>("./PlayerControl/Damage");
                    if (playerDamage != null && !playerDamage.IsDead)
                    {
                        playerDamage.Die();
                    }
                }
                PlayerUI.CurrentTime = 0;
            }
            else
            {
                PlayerUI.CurrentTime = MaxTime - ElapsedTime;
            }
        }
        else
        {
            PlayerUI.CurrentTime = ElapsedTime;
        }

        EnemyDistanceCulling();
        ItemDistanceCulling(false);
    }

    /*
     *	This is an optimization that allows you to place more enemies on the level without having to worry about dedicated spawners.
     *	Enemies which are too far from all players will be frozen in terms of processing.
     */
    void EnemyDistanceCulling()
    {
        foreach (var enemy in EnemyControl.Instances)
        {
            if (enemy.RigidBody == null)
                continue;
            bool closeEnough = false;
            if (enemy.DontCull) // Dead enemies are very likely to be launched outside of the processing range. Let's not stop them frozen in time mid-air and let them depart this world peacefully.
            {
                closeEnough = true;
            }
            if (!closeEnough)
            {
                foreach (var player in PlayerController.Instances)
                {
                    if (
                        player.GlobalPosition.DistanceTo(enemy.RigidBody.GlobalPosition)
                        <= EnemyCullDistance
                    )
                    {
                        closeEnough = true;
                        break;
                    }
                }
            }

            enemy.RigidBody.ProcessMode = closeEnough
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
            enemy.RigidBody.SetPhysicsProcess(closeEnough);
            enemy.RigidBody.Visible = closeEnough || EnemyCulledVisibility;
        }
    }

    void ItemDistanceCulling(bool firstrun)
    {
        if (CollectableItem.Instances.Count == 0)
            return;
        if (ItemDistancesCheckStartFrom >= CollectableItem.Instances.Count || firstrun)
        {
            ItemDistancesCheckStartFrom = 0;
        }

        int endItem;
        if (firstrun)
        {
            endItem = CollectableItem.Instances.Count;
        }
        else
        {
            endItem = Mathf.Min(
                ItemDistancesCheckStartFrom + ItemDistancesCheckedAtOnce,
                CollectableItem.Instances.Count
            );
        }

        for (int i = ItemDistancesCheckStartFrom; i < endItem; i++)
        {
            var item = CollectableItem.Instances[i];
            bool closeEnough = false;
            if (item.DontCull)
            {
                closeEnough = true;
            }
            if (item.AttractiveNode != null) // Items that are attracted are as good as collected. Let's not let players run away from them.
            {
                closeEnough = true;
            }
            if (!closeEnough)
            {
                foreach (var player in PlayerController.Instances)
                {
                    if (player.GlobalPosition.DistanceTo(item.GlobalPosition) <= ItemCullDistance)
                    {
                        closeEnough = true;
                        break;
                    }
                }
            }
            item.ProcessMode = closeEnough ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            item.SetPhysicsProcess(closeEnough);
            item.Visible = closeEnough || ItemCulledVisibility;
        }
        ItemDistancesCheckStartFrom = (ItemDistancesCheckStartFrom + ItemDistancesCheckedAtOnce);
        firstrun = false;
    }
}
