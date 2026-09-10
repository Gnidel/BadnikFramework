using System;
using Godot;

public partial class SuperBossFightStageProperties : Node
{
    [Export]
    public int StartingRings = 50; // It needs to be at least 50 to keep the transformation!

    [Export]
    public bool KillIfNotSuper = true;

    [Export]
    public float SpeedMultiplier = 2f;

    public void OnBattleStart()
    {
        foreach (var player in PlayerController.Instances)
        {
            //player.SpeedMultiplier = SpeedMultiplier;
            player.PlayerInventory.SetItemCount("rings", StartingRings);
            player.MaxSpeed *= SpeedMultiplier;

            var actionSuper = player.GetNodeOrNull<ActionSuperTransformation>(
                new NodePath("./PlayerControl/Actions/ActionSuperTransformation")
            );
            if (actionSuper != null)
            {
                actionSuper.ProcessMode = ProcessModeEnum.Inherit;
                actionSuper.Flying = true;
                actionSuper.SpeedMultiplier = SpeedMultiplier;

                actionSuper.StartTransformation();
            }

            PlayerCamera.Instances[player.PlayerID].MaxCameraDistance *= 5;
            PlayerCamera.Instances[player.PlayerID].Far = 10000; // Space is big but empty
        }

        //SpawnRings();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!KillIfNotSuper || StageData.Instance.ElapsedTime <= 1f || StageData.Instance.IsFinished)
            return;
        foreach (var player in PlayerController.Instances)
        {
            var actionSuper = player.GetNodeOrNull<ActionSuperTransformation>(
                new NodePath("./PlayerControl/Actions/ActionSuperTransformation")
            );
            if (actionSuper != null && !player.PlayerDamage.IsDead)
            {
                if (!actionSuper.IsSuper)
                {
                    player.PlayerDamage.Die();
                }
            }
        }
    }
}
