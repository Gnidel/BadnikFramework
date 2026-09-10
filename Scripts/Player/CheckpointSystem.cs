using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;

public partial class CheckpointSystem : Node
{
    [Export]
    public PlayerController Player;
    public const bool ALWAYS_RESPAWN_NO_RELOAD = false; // Note: Enemies/rings don't respawn in this mode. It's just teleport.
    public static string CheckpointScene;
    public Transform3D DefaultPos;
    public static Transform3D? CheckpointPos;
    public static Transform3D? CheckpointCameraPos;
    public static double CheckpointTime;
    public static Dictionary<int, Dictionary<string, int>> CheckpointItems =
        new Dictionary<int, Dictionary<string, int>>(); // Keys: player, item name
    public static int DeathCount;

    public override void _Ready()
    {
        DefaultPos = Player.GlobalTransform;
        if (StageData.Instance.Style == StageData.StageStyle.ActionStage)
        {
            Respawn();

            StageData.Instance.ElapsedTime = CheckpointTime;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ALWAYS_RESPAWN_NO_RELOAD || StageData.Instance.Style == StageData.StageStyle.HubStage)
        {
            if (Player.PlayerDamage.IsDead)
            {
                Respawn();
            }
        }
        else
        {
            if (PlayerController.Instances.Where(p => !p.NpcPartnerControl.IsNpc).Count() <= 1)
                return;
            if (Player.PlayerDamage.IsDead && !Player.NpcPartnerControl.IsNpc)
            {
                Respawn();
            }
        }
    }

    void Respawn()
    {
        if (CheckpointPos.HasValue)
        {
            Player.GlobalTransform = CheckpointPos.Value;
        }
        else
        {
            Player.GlobalTransform = DefaultPos;
            if (PlayerController.Instances.Where(p => !p.NpcPartnerControl.IsNpc).Count() <= 1)
            {
                DeathCount = 0;
            }
        }
        if (Player.PlayerDamage.IsDead)
        {
            Player.PlayerDamage.Resurrect();
        }
        SetCameraPosition();
    }

    public async void SetCameraPosition()
    {
        await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
        if (CheckpointCameraPos.HasValue && PlayerCamera.Instances.ContainsKey(Player.PlayerID))
        {
            var camera = PlayerCamera.Instances[Player.PlayerID];
            if (camera != null)
            {
                camera.GlobalTransform = CheckpointCameraPos.Value;
            }
        }
    }

    public static void Reset()
    {
        CheckpointTime = 0;
        CheckpointPos = null;
        CheckpointCameraPos = null;
        CheckpointItems.Clear();
        SpecialStageEntrance.CollectedSpecialStagesEntrances.Clear();
    }

    public static void SetItemsFromPlayers()
    {
        SetItemsFromPlayers(new string[0]);
    }

    public static void SetItemsFromPlayers(string[] ignoredItems)
    {
        CheckpointItems = new Dictionary<int, Dictionary<string, int>>();
        foreach (var player in PlayerController.Instances)
        {
            CheckpointItems[player.PlayerID] = new Dictionary<string, int>();
            foreach (var item in player.PlayerInventory.Items)
            {
                if (ignoredItems.Contains(item.Key))
                {
                    CheckpointItems[player.PlayerID][item.Key] = 0;
                }
                else
                {
                    CheckpointItems[player.PlayerID][item.Key] = item.Value;
                }
            }
        }
    }
}
