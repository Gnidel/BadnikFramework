using System;
using System.Xml.Serialization;
using Godot;

public partial class ActionSuperTransformation : Node, IActionUsingHitbox
{
    [Export]
    public bool IsSuper = false;

    [Export]
    public PlayerSkinController NormalSkin;

    [Export]
    public PlayerSkinController SuperSkin;
    private PlayerController player;
    private ActionDash actionDash;
    private double ringsDeductionTimer;

    [Export]
    public float SpeedMultiplier = 1.5f;

    [Export]
    public bool Flying = true;

    [Export]
    public AudioStream TransformationSoundEffect;

    [Export]
    public AudioStreamPlayer3D PlayerSoundEffectsPlayer;

    [Export]
    public AudioStreamPlayer3D SuperPlayer;

    [Export]
    public bool SwitchCamera = true;
    private bool IsTransforming = false;
    private double TransformationTimer = 0;

    [Export]
    public double TransformationDuration = 1;

    [Export]
    public double SwitchModelsAtTransformationTime = 0.5f;

    [Export]
    public Curve TransformationCameraMovement;

    [Export]
    public PathFollow3D TransformationCameraPathFollow;

    [Export]
    public Node3D TransformationCameraPoint;

    [Export]
    public Node3D TransformationCameraLookAt;
    private PlayerCamera.CameraMode PreTransformationCameraMode;
    private Vector3 PreTransformationCameraPosition;
    private Vector3 PreTransformationCameraRotation;
    private Node3D PreTransformationPointCameraPoint;
    private Vector3 PreTransformationVelocity;

    public override void _Input(InputEvent @event)
    {
        if (IsTransforming)
            return;
        if (
            !IsSuper
            && player.PlayerInput.IsActionPressed(@event, "actionsuper")
            && CheckInventoryRequirements()
            && StageData.Instance.Style != StageData.StageStyle.SpecialStage
        )
        {
            StartTransformation();
            //StartSuper();
        }
        else if (IsSuper && player.PlayerInput.IsActionPressed(@event, "actionsuper"))
        {
            StopSuper();
        }
    }

    public override void _Ready()
    {
        player = this.GetNode<PlayerController>("../../..");
        if (IsSuper)
        {
            player.PlayerInventory.SetItemCount("rings", 50);
            StartSuper();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsTransforming)
        {
            TransformationTimer += delta;
            TransformationCameraPathFollow.ProgressRatio = TransformationCameraMovement.SampleBaked(
                (float)Mathf.Clamp(TransformationTimer / TransformationDuration, 0, 1)
            );
            TransformationCameraPoint.LookAt(
                TransformationCameraLookAt.GlobalPosition,
                player.GroundNormal
            );
            if (TransformationTimer >= SwitchModelsAtTransformationTime && !IsSuper)
            {
                PlayerCamera.Instances[player.PlayerID].Filters.EnableImpactFrameFilter();
                StartSuper();
                //player.PlayerSkinController.TravelAnimation("SuperTransformation", true);

                //AnimationNodeStateMachinePlayback normalStateMachine = (AnimationNodeStateMachinePlayback)NormalSkin.AnimationTree.Get("parameters/playback");
                //AnimationNodeStateMachinePlayback superStateMachine = (AnimationNodeStateMachinePlayback)SuperSkin.AnimationTree.Get("parameters/playback");
                //superStateMachine.tra normalStateMachine.GetCurrentPlayPosition();
            }
            if (TransformationTimer >= TransformationDuration)
            {
                EndTransformation();
            }
        }
        if (!IsSuper)
            return;

        if (
            player.PlayerInventory.GetItemCount("rings") <= 0
            || player.PlayerDamage.IsDead
        )
        {
            StopSuper();
            return;
        }

        ringsDeductionTimer += delta;
        if (ringsDeductionTimer >= 1)
        {
            player.PlayerInventory.AddItemCount("rings", -1);
            ringsDeductionTimer -= 1;
        }

        player.PlayerDamage.invincibilityTimer = Mathf.Max(
            1,
            player.PlayerDamage.invincibilityTimer
        );

        if (Flying)
        {
            if (actionDash == null)
            {
                actionDash = player.GetNodeOrNull<ActionDash>("./PlayerControl/Actions/ActionDash");
            }
            if (actionDash != null)
            {
                actionDash.airDashCount = 0;
            }
        }
    }

    public void StartTransformation()
    {
        IsTransforming = true;
        TransformationTimer = 0;
        TransformationCameraPathFollow.ProgressRatio = 0;

        PreTransformationVelocity = player.LinearVelocity;
        player.LinearVelocity = Vector3.Zero;
        player.Enabled = false;

        var cam = PlayerCamera.Instances[player.PlayerID];
        PreTransformationCameraMode = cam.Mode;
        PreTransformationCameraPosition = cam.GlobalPosition;
        PreTransformationCameraRotation = cam.GlobalRotation;
        PreTransformationPointCameraPoint = cam.Point;

        if (SwitchCamera)
        {
            cam.Point = TransformationCameraPoint;
            cam.Mode = PlayerCamera.CameraMode.Point;
        }

        var actionRoll = player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        if (actionRoll != null)
        {
            actionRoll.isRolling = false;
        }
        //player.PlayerSkinController.TravelAnimation("SuperTransformation", false);

        if (NormalSkin != SuperSkin)
        {
            if (NormalSkin != null)
            {
                NormalSkin.ProcessMode = ProcessModeEnum.Inherit;
                NormalSkin.TravelAnimation("Air", true);
                NormalSkin.TravelAnimation("SuperTransformation", false);
            }
            if (SuperSkin != null)
            {
                SuperSkin.ProcessMode = ProcessModeEnum.Inherit;
                SuperSkin.TravelAnimation("Air", true);
                SuperSkin.TravelAnimation("SuperTransformation", false);
            }
        }
    }

    public void EndTransformation()
    {
        IsTransforming = false;

        player.LinearVelocity = PreTransformationVelocity;
        if (player.Grounded)
        {
            player.LinearVelocity = player.VelocityXZ;
        }
        player.Enabled = true;

        if (SwitchCamera)
        {
            var cam = PlayerCamera.Instances[player.PlayerID];
            cam.Mode = PreTransformationCameraMode;
            cam.GlobalPosition = PreTransformationCameraPosition;
            cam.GlobalRotation = PreTransformationCameraRotation;
            cam.Point = PreTransformationPointCameraPoint;
        }

        if (NormalSkin != null)
        {
            NormalSkin.TravelAnimation("Air", false);
        }
        if (SuperSkin != null)
        {
            SuperSkin.TravelAnimation("Air", false);
        }
    }

    public void StopSuper()
    {
        IsSuper = false;
        if (SuperSkin != null)
        {
            SuperSkin.Visible = false;
            SuperSkin.ProcessMode = ProcessModeEnum.Disabled;
        }

        if (Flying)
        {
            player.IsFlying = false;
        }

        if (NormalSkin != null)
        {
            NormalSkin.Visible = true;
            NormalSkin.ProcessMode = ProcessModeEnum.Inherit;
            player.PlayerSkinController = NormalSkin;
            NormalSkin.PlayerController = player;
            NormalSkin.TravelAnimation("Damage");
        }
        player.SpeedMultiplier = 1;
        player.PlayerDamage.BlinkOnInvincibility = true;

        SuperPlayer.Stop();
    }

    public void StartSuper()
    {
        IsSuper = true;
        ringsDeductionTimer =
            1f - Mathf.Clamp(TransformationDuration - SwitchModelsAtTransformationTime, 0, 1);
        if (SuperSkin != null)
        {
            SuperSkin.Visible = true;
            SuperSkin.ProcessMode = ProcessModeEnum.Inherit;
            player.PlayerSkinController = SuperSkin;
            SuperSkin.PlayerController = player;

            AnimationNodeStateMachinePlayback normalStateMachine =
                (AnimationNodeStateMachinePlayback)
                    NormalSkin.AnimationTree.Get("parameters/playback");
            SuperSkin.AnimationTree.Set("parameters/playback", normalStateMachine);
        }

        if (NormalSkin != null && SuperSkin != null && NormalSkin != SuperSkin)
        {
            NormalSkin.Visible = false;
            NormalSkin.ProcessMode = ProcessModeEnum.Disabled;
        }
        player.SpeedMultiplier = this.SpeedMultiplier;
        player.PlayerDamage.BlinkOnInvincibility = false;

        PlayerSoundEffectsPlayer.Stream = TransformationSoundEffect;
        PlayerSoundEffectsPlayer.Play(0);

        if (Flying)
        {
            player.IsFlying = true;
        }

        SuperPlayer.Play();
    }

    public bool CheckInventoryRequirements()
    {
        if (player.PlayerInventory.GetItemCount("rings") < 50)
            return false;
        string[] emeralds =
        {
            "chaosemerald_red",
            "chaosemerald_blue",
            "chaosemerald_yellow",
            "chaosemerald_green",
            "chaosemerald_gray",
            "chaosemerald_cyan",
            "chaosemerald_magenta",
        };
        foreach (var emerald in emeralds)
        {
            if (player.PlayerInventory.GetItemCount(emerald) <= 0)
                return false;
        }

        return true;
    }

    public bool IsHitboxEnabled()
    {
        return IsSuper;
    }
}
