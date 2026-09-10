using System;
using Godot;

public partial class PlayerSkinController : Node3D
{
    [Export]
    public PlayerController PlayerController;

    [Export]
    public AnimationTree AnimationTree;

    [Export]
    public AnimationPlayer AnimationPlayer;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public ActionGrind ActionGrind;

    [Export]
    public ActionDrift ActionDrift;

    [Export]
    public ActionGun ActionGun;

    [Export]
    public ActionSkate ActionSkate;
    private WaterInteraction WaterInteraction;

    [Export]
    public GpuParticles3D RunWindParticles;

    [Export]
    public GpuParticles3D RunDustParticles;

    [Export]
    public GpuParticles3D RunSplashParticles;

    [Export]
    public AudioStreamPlayer3D RunWind;

    [Export]
    public Node3D Aura;

    [Export]
    public Color RunWindColor = new Color(1, 1, 1);

    [Export]
    public MeshInstance3D SpinBall;

    [Export]
    public MeshInstance3D SpinDashBall;

    [Export]
    public Node3D[] Models = new Node3D[0];

    [Export]
    public float SpinBallRotationSpeed = 10f;

    [Export]
    public float RunSpeedAnimationMax = 30f;

    [Export]
    public Node3D HeadCameraFocusPoint;

    [Export]
    public bool SkipRankWaitAnimation = false;

    [Export]
    public Node3D BoneAttachmentSkateboard;

    private string prevState;

    public override void _Process(double delta)
    {
        if (ActionRoll == null)
        {
            ActionRoll = PlayerController.GetNodeOrNull<ActionRoll>(
                "./PlayerControl/Actions/ActionRoll"
            );
        }
        if (ActionGrind == null)
        {
            ActionGrind = PlayerController.GetNodeOrNull<ActionGrind>(
                "./PlayerControl/Actions/ActionGrind"
            );
        }
        if (ActionGun == null)
        {
            ActionGun = PlayerController.GetNodeOrNull<ActionGun>(
                "./PlayerControl/Actions/ActionGun"
            );
        }
        if (WaterInteraction == null)
        {
            WaterInteraction = PlayerController.GetNodeOrNull<WaterInteraction>(
                "./PlayerControl/WaterInteraction"
            );
        }
        if (ActionSkate == null)
        {
            ActionSkate = PlayerController.GetNodeOrNull<ActionSkate>(
                "./PlayerControl/Actions/ActionSkate"
            );
        }

        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            AnimationTree.Get("parameters/playback");
        if (PlayerController.PlayerDamage.IsDead)
        {
            stateMachine.Travel("Dead");
            RunWind.Stop();
            RunWindParticles.Emitting = false;
            RunDustParticles.Emitting = false;
            RunSplashParticles.Emitting = false;
            return;
        }

        if (ActionSkate != null && ActionSkate.IsSkating)
        {
            SkatingAnimations(delta);
        }
        else
        {
            RegularAnimations(delta);
        }
    }

    private void RegularAnimations(double delta)
    {
        AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
            AnimationTree.Get("parameters/playback");

        if (PlayerController.PlayerDamage.IsDamaged)
        {
            stateMachine.Travel("Damage");
        }
        if (ActionGrind.IsGrinding())
        {
            if (stateMachine.GetCurrentNode().ToString().Substr(0, "Grind".Length) != "Grind")
            {
                stateMachine.Travel("Grinding");
            }
            AnimationTree.Set(
                "parameters/conditions/isGrinding",
                ActionGrind.IsGrinding() && !ActionGrind.IsCrouching
            );
            AnimationTree.Set(
                "parameters/conditions/isGrindingSquating",
                ActionGrind.IsGrinding() && ActionGrind.IsCrouching
            );
        }
        else if (
            stateMachine.GetCurrentNode() == "Grinding"
            || stateMachine.GetCurrentNode() == "GrindSquat"
        )
        {
            stateMachine.Travel("Air");
        }

        AnimationTree.Set(
            "parameters/conditions/isIdle",
            PlayerController.Grounded
                && PlayerController.VelocityXZ.Length() <= 0.1f
                && !ActionRoll.isRolling
                && !ActionGrind.IsGrinding()
        );
        AnimationTree.Set(
            "parameters/conditions/isRunning",
            PlayerController.Grounded
                && PlayerController.VelocityXZ.Length() > 0.1f
                && !ActionRoll.isRolling
                && !ActionGrind.IsGrinding()
        );
        AnimationTree.Set(
            "parameters/conditions/isAir",
            !PlayerController.Grounded && !ActionRoll.isRolling && !ActionGrind.IsGrinding()
        );

        AnimationTree.Set("parameters/conditions/isRolling", ActionRoll.isRolling);
        if (ActionRoll.isRolling)
        {
            stateMachine.Travel("Rolling");
        }

        if (ActionDrift != null)
        {
            AnimationTree.Set("parameters/Running/drift/blend_amount", ActionDrift.VisualDriftDir);
        }

        float runAnim =
            Mathf.Clamp(
                PlayerController.VelocityXZ.Length() / PlayerController.SpeedMultiplier,
                0,
                30
            ) / 15
            - 1;

        AnimationTree.Set("parameters/Running/runAnim/blend_amount", runAnim);
        float currLeaning = AnimationTree
            .Get("parameters/Running/leaning_highspeed/blend_amount")
            .AsSingle();

        AnimationTree.Set(
            "parameters/Running/leaning_highspeed/blend_amount",
            Mathf.Lerp(currLeaning, PlayerController.Leaning, (float)delta * 5)
        );
        AnimationTree.Set(
            "parameters/Running/leaning_midspeed/blend_amount",
            Mathf.Lerp(currLeaning, PlayerController.Leaning, (float)delta * 5)
        );

        var speedScale = 1f;
        const float scaleBaseline = 10;
        if (PlayerController.Grounded && PlayerController.VelocityXZ.Length() > scaleBaseline)
        {
            speedScale =
                PlayerController.VelocityXZ.Length()
                / PlayerController.SpeedMultiplier
                / scaleBaseline;
        }
        speedScale = Mathf.Clamp(speedScale, 0, RunSpeedAnimationMax);
        AnimationTree.Set("parameters/Running/timeScale/scale", speedScale);
        var rollRpeedScale = 1f;

        if (ActionRoll.IsSpinDashing)
        {
            rollRpeedScale = 100f;
            AnimationTree.Set("parameters/Rolling/isSquating/blend_amount", 0);
        }
        else
        {
            AnimationTree.Set(
                "parameters/Rolling/isSquating/blend_amount",
                !PlayerController.Grounded || PlayerController.VelocityXZ.Length() > 0.1f ? 0 : 1
            );
        }

        if (PlayerController.Grounded && !ActionRoll.IsSpinDashing)
        {
            rollRpeedScale = Mathf.Clamp(PlayerController.VelocityXZ.Length() / 2 / Mathf.Pi, 0, 1);
            if (PlayerController.VelocityXZ.Normalized().Dot(-PlayerController.Basis.Z) < 0)
            {
                rollRpeedScale *= -1;
            }
        }
        AnimationTree.Set("parameters/Rolling/timeScale/scale", rollRpeedScale);
        AnimationTree.Set(
            "parameters/Air/velocityY/blend_amount",
            Mathf.Clamp(PlayerController.VelocityYDirected / 30, 0, 1)
        );
        AnimationTree.Set(
            "parameters/Air/velocityXZ/blend_amount",
            Mathf.Clamp(PlayerController.VelocityXZ.Length() / 30, 0, 1)
        );

        if (ActionGun != null)
        {
            AnimationTree.Set("parameters/Gun Fire/runAnim/blend_amount", runAnim);
        }

        SetParticles(delta);
    }

    private void SkatingAnimations(double delta)
    {
        try
        {
            AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
                AnimationTree.Get("parameters/playback");

            if (ActionSkate.IsTricking) { }
            else if (ActionGrind != null && ActionGrind.IsGrinding())
            {
                stateMachine.Travel("Skate Grind");
                AnimationTree.Set(
                    "parameters/Skate Grind/leaning/blend_amount",
                    PlayerController.Leaning
                );
            }
            else if (PlayerController.Grounded)
            {
                stateMachine.Travel("Skate Ground");

                AnimationTree.Set(
                    "parameters/Skate Ground/leaning_highspeed/blend_amount",
                    PlayerController.Leaning
                );
                AnimationTree.Set(
                    "parameters/Skate Ground/leaning_midspeed/blend_amount",
                    PlayerController.Leaning
                );
                float runAnim =
                    Mathf.Clamp(
                        PlayerController.VelocityXZ.Length() / PlayerController.SpeedMultiplier,
                        0,
                        30
                    ) / 15
                    - 1;
                AnimationTree.Set("parameters/Skate Ground/runAnim/blend_amount", runAnim);
            }
            else
            {
                stateMachine.Travel("Skate Air");
                AnimationTree.Set(
                    "parameters/Skate Air/velocityY/blend_amount",
                    Mathf.Clamp(PlayerController.VelocityYDirected / 30 / 2, -0.5f, 0.5f) + 0.5f
                );
            }
        }
        catch (Exception e)
        {
            // This is to prevent abortion of scripts using TravelAnimation() if the animation doesn't exist.
            GD.PrintErr(e.ToString());
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        //CorrectClipping(delta);
    }

    private void CorrectClipping(double delta)
    {
        /*	Technically, our character flies a bit above the ground instead of touching it. It is to make collission better on small steps, like
            on small stairs. However, this looks can result in looking wrong (flying a bit above ground or clipping), so we make it not LOOK wrong.
            Players don't have to know.
            Note that the raycast is done at the start of player physics calculations, so it's a frame delayed. That's why we keep XZ position and adjust Y position.
         */
        if (
            PlayerController.Grounded
            && PlayerController.GroundRayHitPoint.HasValue
            && !ActionGrind.IsGrinding()
        )
        {
            this.GlobalPosition =
                Vector3Utils.ProjectOnPlane(
                    PlayerController.GlobalPosition,
                    PlayerController.Basis.Y
                )
                + Vector3Utils.Project(
                    PlayerController.GroundRayHitPoint.Value
                        + PlayerController.lastMovingPlatformCorrection * 2,
                    PlayerController.Basis.Y
                );
        }
        else
            this.GlobalPosition = PlayerController.GlobalPosition;
    }

    public void TravelAnimation(string animationName, bool forceAnimRestart = false)
    {
        try
        {
            AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
                AnimationTree.Get("parameters/playback");
            stateMachine.Travel(animationName);
            if (forceAnimRestart)
            {
                stateMachine.ResetState();
                stateMachine.Start(animationName, true);
            }
        }
        catch (Exception e)
        {
            // This is to prevent abortion of scripts using TravelAnimation() if the animation doesn't exist.
            //GD.PrintErr(e.ToString());
        }
    }

    public double GetParameter(string paramName)
    {
        return AnimationTree.Get(paramName).AsDouble();
    }

    public void SetParameter(string paramName, double value)
    {
        AnimationTree.Set(paramName, value);
    }

    private void SetParticles(double delta)
    {
        if (RunWindParticles != null)
        {
            RunWindParticles.Emitting = PlayerController.LinearVelocity.Length() > 30;
            RunWindParticles.ProcessMaterial.Set(
                "color",
                new Color(
                    RunWindColor.R,
                    RunWindColor.G,
                    RunWindColor.B,
                    (PlayerController.LinearVelocity.Length() - 30) / 100f
                )
            );
        }
        if (RunWind != null)
        {
            if (PlayerController.LinearVelocity.Length() > 30)
            {
                if (!RunWind.Playing)
                {
                    RunWind.Play();
                }
                RunWind.VolumeDb = Mathf.Lerp(
                    RunWind.VolumeDb,
                    -80 + PlayerController.LinearVelocity.Length(),
                    (float)delta * 5
                );
            }
            else
            {
                RunWind.Playing = false;
                RunWind.VolumeDb = -80;
            }
        }

        bool isSteppingInWater = (WaterInteraction != null && WaterInteraction.IsSteppingInWater());
        if (isSteppingInWater)
        {
            if (RunSplashParticles != null)
            {
                RunSplashParticles.Emitting =
                    PlayerController.Grounded
                    && PlayerController.VelocityXZ.Length() > 0
                    && !PlayerController.PlayerDamage.IsDead;
                RunSplashParticles.ProcessMaterial.Set(
                    "scale_max",
                    (PlayerController.VelocityXZ.Length()) / 20f
                );
                RunDustParticles.Emitting = false;
            }
        }
        else
        {
            if (RunDustParticles != null)
            {
                RunDustParticles.Emitting =
                    PlayerController.Grounded && PlayerController.LinearVelocity.Length() > 30;
                RunDustParticles.ProcessMaterial.Set(
                    "scale_max",
                    (PlayerController.LinearVelocity.Length() - 30) / 10f
                );
                RunSplashParticles.Emitting = false;
            }
        }

        if (Aura != null)
        {
            var target = PlayerController
                .Gravity.Normalized()
                .Lerp(
                    PlayerController.LinearVelocity.Normalized(),
                    Mathf.Clamp((PlayerController.LinearVelocity.Length() - 10) / 10f, 0f, 1)
                )
                .Normalized();
            //Vector3 target = PlayerController.Gravity.Normalized();
            //if (PlayerController.LinearVelocity.Length() > 10)
            //{
            //	target = PlayerController.LinearVelocity.Normalized();
            //}
            target = (-Aura.GlobalBasis.Z)
                .Lerp(target, Mathf.Clamp((float)delta * 10, 0, 1))
                .Normalized();
            Aura.LookAt(Aura.GlobalPosition + target, target.Cross(Aura.GlobalBasis.X));

            Aura.Scale = new Vector3(
                1,
                1,
                Mathf.Clamp(PlayerController.LinearVelocity.Length() / 50, 1f, 2)
            );
        }

        if (SpinBall != null)
        {
            if (SpinBall.Visible)
            {
                SpinBall.RotateX(Mathf.Pi * -2f * SpinBallRotationSpeed * (float)delta);
            }
            foreach (var model in Models)
            {
                model.Visible = !SpinBall.Visible && !SpinDashBall.Visible;
            }
        }
    }
}
