using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ActionGrind : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public PlayerInput PlayerInput;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public ActionHoming ActionHoming;

    [Export]
    public float MaxRailStartDistance = 10f;

    [Export]
    public AudioStream GrindStartSoundEffect;

    [Export]
    public AudioStream GrindSoundEffect;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStreamPlayer3D ContinousAudioPlayer;
    PathFollow3D PathFollower;

    [Export]
    public GpuParticles3D GrindTrail;

    [Export]
    public GpuParticles3D[] GrindParticles = new GpuParticles3D[0];

    [Export]
    public double GrindCooldown = 0.1f;

    [Export]
    public float GravityImpact = 5f;

    [Export]
    public float CrouchedGravityImpact = 60f;

    public bool IsCrouching;

    private double grindCooldownTimer;
    private bool lookForward = true;
    private bool playedSound = false;

    [Export]
    public HomingTarget HomingTarget;

    public List<GrindRail> Swaps = new List<GrindRail>();

    public float railSwapWeight = 1;

    public bool IsGrinding()
    {
        return PathFollower != null;
    }

    public void CopyStateFrom(ActionGrind source)
    {
        if (!source.IsGrinding())
            return;

        var sourceFollower = source.PathFollower;
        var rail = sourceFollower.GetParent<GrindRail>();
        var follower = new PathFollow3D
        {
            Loop = sourceFollower.Loop,
        };
        rail.AddChild(follower);
        follower.Progress = sourceFollower.Progress;
        follower.GlobalTransform = sourceFollower.GlobalTransform;
        follower.ResetPhysicsInterpolation();

        StartGrind(follower, follower.GlobalTransform);
        IsCrouching = source.IsCrouching;
        railSwapWeight = source.railSwapWeight;
    }

    public bool CanGrind()
    {
        return !IsGrinding() && grindCooldownTimer <= 0;
    }

    public override void _Process(double delta)
    {
        if (IsGrinding())
        {
            IsCrouching = PlayerInput.IsActionPressed("actionroll");
        }
        else
        {
            playedSound = false;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (delta == 0)
            return;

        grindCooldownTimer = IsGrinding() ? GrindCooldown : grindCooldownTimer - delta;
        railSwapWeight = Mathf.Clamp(railSwapWeight + (float)delta * 2f, -1, 1);
        RailSwapSystem();

        if (PathFollower != null)
        {
            var initialVelocity = Player.LinearVelocity;

            if (!ContinousAudioPlayer.Playing && railSwapWeight >= 1 && !playedSound)
            {
                playedSound = true;
                ContinousAudioPlayer.Stream = GrindSoundEffect;
                ContinousAudioPlayer.Play();

                AudioPlayer.Stream = GrindStartSoundEffect;
                AudioPlayer.Play();

                SetGrindParticlesEmitting(true);
            }

            GrindTrail.ProcessMaterial.Set(
                "initial_velocity_min",
                Vector3Utils.LengthAlongsideVector(this.Player.LinearVelocity, -Player.Basis.Z)
            );
            GrindTrail.ProcessMaterial.Set(
                "initial_velocity_max",
                Vector3Utils.LengthAlongsideVector(this.Player.LinearVelocity, -Player.Basis.Z)
            );

            var progress =
                this.PathFollower.Progress
                + (-PathFollower.Basis.Z.Normalized()).Dot(Player.LinearVelocity.Normalized())
                    * Player.LinearVelocity.Length()
                    * (float)delta;

            if (!Player.Grounded || Player.PlayerDamage.IsDead)
            {
                StopGrind();
                return;
            }
            if (
                (
                    progress >= this.PathFollower.GetParent<Path3D>().Curve.GetBakedLength()
                    || progress <= 0
                ) && !this.PathFollower.Loop
            )
            {
                this.Player.PlayerInput.LeftInputTimedLock = 0.0f;

                this.Player.PlayerInput.LeftInput3D =
                    -this.PathFollower.Basis.Z.Normalized() * (progress > 0 ? 1 : -1);
                StopGrind();
                return;
            }
            this.PathFollower.Progress = progress;

            if (railSwapWeight >= 1)
            {
                Player.GlobalPosition = this.PathFollower.GlobalPosition;
            }
            else
            {
                Player.GlobalPosition = Player.GlobalPosition.Lerp(
                    this.PathFollower.GlobalPosition,
                    Mathf.Clamp(railSwapWeight, 0, 1)
                );
            }
            Player.GlobalRotation = this.PathFollower.GlobalRotation;
            //Player.GlobalRotation = this.PathFollower.GetNode<Path3D>("..").Curve.SampleBakedWithRotation(this.PathFollower.Progress).Basis.GetRotationQuaternion().GetEuler(); ;
            if (!lookForward)
                Player.Rotate(this.PathFollower.Basis.Y.Normalized(), Mathf.DegToRad(180));
            Player.GroundNormal = this.PathFollower.Basis.Y.Normalized();

            Player.LinearVelocity =
                -this.PathFollower.Basis.Z.Normalized()
                * Player.LinearVelocity.Length()
                * (-PathFollower.Basis.Z.Normalized()).Dot(Player.LinearVelocity.Normalized());

            if (IsCrouching)
            {
                Player.LinearVelocity +=
                    -this.PathFollower.Basis.Z.Normalized()
                    * (-this.PathFollower.Basis.Z.Normalized()).Dot(Player.Gravity.Normalized())
                    * CrouchedGravityImpact
                    * (float)delta;
            }
            else
            {
                Player.LinearVelocity +=
                    -this.PathFollower.Basis.Z.Normalized()
                    * (-this.PathFollower.Basis.Z.Normalized()).Dot(Player.Gravity.Normalized())
                    * GravityImpact
                    * (float)delta;
            }

            var newLeaning = (Player.LinearVelocity - initialVelocity)
                .Normalized()
                .Dot(Player.GlobalBasis.X);

            Player.Leaning = Mathf.Lerp(Player.Leaning, newLeaning, (float)delta);
        }
    }

    public void RailSwapSystem()
    {
        if (!IsGrinding())
        {
            HomingTarget.Visible = false;
            return;
        }

        if (Swaps.Count > 0)
        {
            bool quickStepL = Player.PlayerInput.IsActionJustPressed("quickstep_l");
            bool quickStepR = Player.PlayerInput.IsActionJustPressed("quickstep_r");

            if (
                quickStepL
                || quickStepR
                || Mathf.Abs(PlayerInput.LeftRawInput.X) >= 0.5f && railSwapWeight >= 1
            )
            {
                //GrindRail? bestSwap = null;
                //var nearestDistance = 0f;
                foreach (var swap in Swaps)
                {
                    if (swap == this.PathFollower.GetParent<GrindRail>())
                        continue;
                    var nearestPoint =
                        swap.Curve.GetClosestPoint(Player.GlobalPosition - swap.GlobalPosition)
                        + swap.GlobalPosition;
                    var offset = (nearestPoint - Player.GlobalPosition);
                    PlayerCamera cam = PlayerCamera.Instances.GetValueOrDefault(
                        Player.PlayerID,
                        null
                    );

                    if (quickStepL)
                    {
                        if (cam == null || offset.Normalized().Dot(-cam.GlobalBasis.X) < 0)
                        {
                            continue;
                        }
                    }
                    else if (quickStepR)
                    {
                        if (cam == null || offset.Normalized().Dot(cam.GlobalBasis.X) < 0)
                        {
                            continue;
                        }
                    }
                    else // Analog stick move
                    {
                        if (offset.Normalized().Dot(PlayerInput.LeftInput3D.Normalized()) < 0)
                        {
                            continue;
                        }
                    }

                    var animName = "Grinding Switch R";
                    if (offset.Normalized().Dot(Player.Basis.X) <= 0)
                    {
                        animName = "Grinding Switch L";
                    }

                    swap.StartGrind(Player, this);

                    Player.PlayerSkinController.TravelAnimation(animName, true);
                    railSwapWeight = -0.5f;
                    grindCooldownTimer = GrindCooldown;
                    ContinousAudioPlayer.Stop();
                    playedSound = false;

                    SetGrindParticlesEmitting(false);

                    break;
                }
            }
        }
        else
        {
            HomingTarget.Visible = false;
        }
    }

    public void StartGrind(PathFollow3D follower, Transform3D startPointSample)
    {
        this.PathFollower = follower;

        Player.Enabled = false;
        Player.Grounded = true;
        if (ActionRoll.isRolling)
        {
            ActionRoll.StopRoll();
        }

        ActionHoming.IsHoming = false;
        railSwapWeight = 1;

        lookForward =
            (-startPointSample.Basis.Z.Normalized()).Dot(Player.LinearVelocity.Normalized()) >= 0;
    }

    public void StopGrind()
    {
        this.PathFollower.QueueFree();

        this.PathFollower = null;

        Player.Enabled = true;
        ContinousAudioPlayer.Stop();
        SetGrindParticlesEmitting(false);
    }

    public void OnSwapRailEnter(Area3D other)
    {
        var newRail = other.GetParentOrNull<GrindRail>();
        if (newRail != null)
        {
            Swaps.Add(newRail);
        }
    }

    public void OnSwapRailExit(Area3D other)
    {
        var newRail = other.GetParentOrNull<GrindRail>();
        if (newRail != null)
        {
            Swaps.Remove(newRail);
        }
    }

    public void SetGrindParticlesEmitting(bool emit)
    {
        if (emit && !GrindTrail.Emitting)
        {
            GrindTrail.Restart();
        }
        GrindTrail.Emitting = emit;
        foreach (var particle in GrindParticles)
        {
            particle.Emitting = emit;
        }
    }
}
