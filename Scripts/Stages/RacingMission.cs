using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

// NOTE: THIS CODE IS AN UNFINISHED LEFTOVER FROM SHADOW R, a RAGE game. It's enough to make characters run along the specified paths, but it's very simplistic.
public partial class RacingMission : Node3D
{
    [Export]
    public Path3D RacingPath;

    [Export]
    public bool Loop = true;

    [Export]
    public int Laps = 10;

    [Export]
    public RichTextLabel RankingLabel;

    [Export]
    public RichTextLabel[] PlayerPosLabels = new RichTextLabel[0];

    //[Export] public float[] PlayerFurthestOffset = new float[4];
    //[Export] public float[] PlayerCurrentOffset = new float[4];
    //[Export] public float[] PlayerRanking = new float[4];
    //[Export] public int[] PlayerLaps = new int[4];
    private PlayerStatus[] PlayerStatuses;

    class PlayerStatus
    {
        public string Name;
        public int PlayerID;
        public float FurthestOffset;
        public float CurrentOffset;
        public float Ranking;
        public int Lap;
    }

    public override void _Ready()
    {
        StartRace();
    }

    void StartRace()
    {
        foreach (var label in PlayerPosLabels)
        {
            label.Text = "";
        }

        // Add enough labels for all players
        var firstLabel = PlayerPosLabels[0];
        PlayerPosLabels = new RichTextLabel[PlayerController.Instances.Count];
        PlayerPosLabels[0] = firstLabel;

        PlayerStatuses = new PlayerStatus[PlayerController.Instances.Count];
        for (int i = 1; i < PlayerController.Instances.Count; i++)
        {
            var newLabel = PlayerPosLabels[0].Duplicate().GetNode<RichTextLabel>(".");
            PlayerPosLabels[0].AddSibling(newLabel);
            PlayerPosLabels[i] = newLabel;
        }
        foreach (var player in PlayerController.Instances)
        {
            // Add NPC follow target
            var targetNode = new Node3D();
            this.AddChild(targetNode);
            player.NpcPartnerControl.Target = targetNode;

            // Add icons to UI
            PlayerPosLabels[player.PlayerID].Text =
                "[color="
                + player.CharacterThemeColor.ToHtml(false)
                + "]"
                + player.CharacterName.Substr(0, 1)
                + "[/color]";

            // Add status
            PlayerStatuses[player.PlayerID] = new PlayerStatus();
            PlayerStatuses[player.PlayerID].Name = player.CharacterName;
            PlayerStatuses[player.PlayerID].PlayerID = player.PlayerID;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        NpcMovement(delta);
    }

    int currentlyProcessedPlayer;

    void NpcMovement(double delta)
    {
        currentlyProcessedPlayer++;
        if (currentlyProcessedPlayer >= PlayerController.Instances.Count)
            currentlyProcessedPlayer = 0;

        //foreach (var player in PlayerController.Instances)
        {
            var player = PlayerController.Instances[currentlyProcessedPlayer];
            // Set NPC target locations
            //if (!player.NpcPartnerControl.IsNpc) continue;
            var relativePlayerPosition = RacingPath.ToLocal(player.GlobalPosition);

            var nearestOffset = RacingPath.Curve.GetClosestOffset(relativePlayerPosition);

            if (
                PlayerStatuses[player.PlayerID].CurrentOffset / RacingPath.Curve.GetBakedLength()
                    > 0.9f
                && nearestOffset / RacingPath.Curve.GetBakedLength() < 0.1f
            )
            {
                PlayerStatuses[player.PlayerID].Lap++;
            }
            else if (
                PlayerStatuses[player.PlayerID].CurrentOffset / RacingPath.Curve.GetBakedLength()
                    < 0.1f
                && nearestOffset / RacingPath.Curve.GetBakedLength() > 0.9f
            )
            {
                PlayerStatuses[player.PlayerID].Lap--;
            }

            PlayerStatuses[player.PlayerID].CurrentOffset = nearestOffset;

            PlayerStatuses[player.PlayerID].FurthestOffset = Mathf.Max(
                nearestOffset,
                nearestOffset
            );
            nearestOffset = PlayerStatuses[player.PlayerID].FurthestOffset;

            var rubberbandFactor = 1;

            var nextPosition = RacingPath.Curve.SampleBaked(nearestOffset + 10f);
            var nextPositionWorldSpace = RacingPath.ToGlobal(nextPosition);

            var spaceState = player.GetWorld3D().DirectSpaceState;
            var rayQuery = PhysicsRayQueryParameters3D.Create(
                nextPositionWorldSpace,
                nextPositionWorldSpace - player.GlobalBasis.Y * 150f
            );
            rayQuery.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
            var rayResult = spaceState.IntersectRay(rayQuery);

            if (rayResult.Count > 0)
            {
                nextPositionWorldSpace = rayResult["position"].AsVector3();
            }

            player.NpcPartnerControl.Target.GlobalPosition = nextPositionWorldSpace;
            var checkpointSystem = player.GetNodeOrNull<CheckpointSystem>(
                "./PlayerControl/CheckpointSystem"
            );
            if (checkpointSystem != null)
            {
                checkpointSystem.DefaultPos.Origin = nextPositionWorldSpace;
            }

            // Set tracker positions
            var progressNormalized = nearestOffset / RacingPath.Curve.GetBakedLength();
            PlayerPosLabels[player.PlayerID].Position = new Vector2(progressNormalized * 1000f, 0);
        }

        // Make ranking
        var ranking = new SortedList<float, PlayerStatus>(); // Key: progress, value: player name
        foreach (var player in PlayerController.Instances)
        {
            try
            {
                ranking.Add(
                    PlayerStatuses[player.PlayerID].CurrentOffset
                        + RacingPath.Curve.GetBakedLength() * PlayerStatuses[player.PlayerID].Lap,
                    PlayerStatuses[player.PlayerID]
                );
            }
            catch (ArgumentException) // Happens when multiple players have same position (usually 0). Let's solve it by changing position a bit
            {
                ranking.Add(
                    PlayerStatuses[player.PlayerID].CurrentOffset
                        + RacingPath.Curve.GetBakedLength() * PlayerStatuses[player.PlayerID].Lap
                        - Mathf.Epsilon * player.GlobalPosition.Length() * player.PlayerID,
                    PlayerStatuses[player.PlayerID]
                );
            }
        }

        var rankingText = "";
        for (int i = 0; i < ranking.Count; i++)
        {
            rankingText +=
                (i + 1).ToString()
                + ". "
                + ranking.Values[ranking.Count - i - 1].Name
                + " (Lap "
                + ranking.Values[ranking.Count - i - 1].Lap.ToString()
                + ")\n";
        }
        RankingLabel.Text = rankingText;
    }
}
