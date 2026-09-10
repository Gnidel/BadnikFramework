using System;
using System.Collections.Generic;
using Godot;

public partial class ActionHoming : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public ActionJump ActionJump;

    [Export]
    public ActionDash ActionDash;

    //[Export] public Camera3D Camera;
    public Node3D MainTarget;
    public List<HomingTarget> Targets = new List<HomingTarget>();

    //[Export] public Node2D HomingIcon;
    [Export]
    public float HomingVelocity = 100;
    private bool _ishoming;

    [Export]
    public bool IsHoming
    {
        get { return _ishoming; }
        set
        {
            bool isStart = (_ishoming == false && value == true);
            bool isEnd = (_ishoming == true && value == false);
            if (DisappearDuringHoming)
            {
                if (isStart)
                {
                    Player.PlayerSkinController.Visible = false;
                    Player.PlayerDamage.ProcessMode = ProcessModeEnum.Disabled;
                }
                else if (isEnd)
                {
                    Player.PlayerSkinController.Visible = true;
                    Player.PlayerDamage.ProcessMode = ProcessModeEnum.Inherit;
                }
            }
            if (StartParticles != null && isStart)
            {
                var particle = StartParticles.Instantiate<MultiParticlePlayer>();
                GetTree().Root.AddChild(particle);
                particle.GlobalPosition = this.Player.GlobalPosition;
                particle.Play();
            }
            if (EndParticles != null && isEnd)
            {
                var particle = EndParticles.Instantiate<MultiParticlePlayer>();
                GetTree().Root.AddChild(particle);
                particle.GlobalPosition = this.Player.GlobalPosition;
                particle.Play();
            }
            _ishoming = value;
        }
    }

    [Export]
    public AudioStream TargetBeep;

    [Export]
    public AudioStream HomingSound;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public double MaxHomingAttackTime = 3f; // In case of being stuck
    private double homingAttackTimer = 0;

    [Export]
    public bool DisappearDuringHoming = false;

    [Export]
    public PackedScene StartParticles;

    [Export]
    public PackedScene EndParticles;

    [Export]
    public bool SonicGTHomingAttack = true; // Hold homing attack button to keep the momentum going
    public float sonicGTHomingAttackSpeed;

    [Export]
    public bool HomingAttackWithJumpButton = false;
    public const string JUMPINPUTNAME = "actionjump";
    public const string HOMINGINPUTNAME = "actionhoming";

    public override void _Input(InputEvent @event)
    {
        /*	Known bug: if you have double jump enabled in ActionJump and do homing attack with jump button, then
            there will be a sound of jump instead of homing attack.
            I'm not fixing it on purpose to discourage mapping homing attack to the same button as double jump.
            I hated that in Forces and Colors. */
        var isHomingPressed = HomingAttackWithJumpButton
            ? Player.PlayerInput.IsActionPressed(@event, HOMINGINPUTNAME)
                || (
                    !Player.Grounded
                    && Player.PlayerInput.IsActionPressed(@event, JUMPINPUTNAME)
                    && ActionJump.JumpCount >= ActionJump.MaxJumpCount
                )
            : Player.PlayerInput.IsActionPressed(@event, HOMINGINPUTNAME);

        if (isHomingPressed && MainTarget != null && !IsHoming)
        {
            StartHoming();
        }
    }

    public override void _Process(double delta)
    {
        if (Player.NpcPartnerControl.IsNpc)
            return;
        if (StageData.Instance != null && StageData.Instance.IsFinished)
        {
            MainTarget = null;
            IsHoming = false;
        }
        if (
            !IsInstanceValid(MainTarget) /*|| !MainTarget.IsVisibleInTree()*/
        )
        {
            MainTarget = null;
            IsHoming = false;
        }

        var HomingIcon = OnePlayerUI.Instances[Player.PlayerID].HomingIcon;
        var HomingOffscreenIcon = OnePlayerUI.Instances[Player.PlayerID].HomingOffscreenIcon;

        if (!IsHoming && Targets.Count > 0)
        {
            var targetsCopy = Targets; // To not break foreach after deleting
            foreach (var target in targetsCopy)
            {
                if (
                    !IsInstanceValid(target) /*|| !target.IsVisibleInTree()*/
                )
                {
                    Targets.Remove(target);
                    return; // We'll deal with other bad targets on the next frame
                }
            }

            var bestTargetScore = Mathf.Inf;
            Node3D bestTarget = null;
            foreach (var target in Targets)
            {
                if (!target.IsVisibleInTree() || !target.TargetableByHomingAttack)
                {
                    continue;
                }

                var spaceState = Player.GetWorld3D().DirectSpaceState;
                var rayQuery = PhysicsRayQueryParameters3D.Create(
                    Player.GlobalPosition + Player.Basis.Y * 0.5f,
                    target.GlobalPosition
                );
                rayQuery.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
                var rayResult = spaceState.IntersectRay(rayQuery);

                if (rayResult.Count > 0)
                {
                    if (rayResult["collider"].AsGodotObject() is Node3D collider)
                    {
                        if (
                            !(
                                collider.IsAncestorOf(target)
                                || collider.GetParent().IsAncestorOf(target)
                                || rayResult["position"]
                                    .AsVector3()
                                    .DistanceTo(target.GlobalPosition) < 0.5f
                            )
                        )
                        {
                            continue;
                        }
                    }
                }

                var thisScore = GetTargetScore(target);
                if (Player.PlayerInput.LeftInput3D.Length() > 0.5f)
                {
                    var targetOffset = target.GlobalPosition - Player.GlobalPosition;
                    if (
                        Player.PlayerInput.LeftInput3D.Normalized().Dot(targetOffset.Normalized())
                        < 0.5f
                    )
                    {
                        thisScore *= 10000f;
                    }
                }
                if (thisScore < bestTargetScore)
                {
                    bestTargetScore = thisScore;
                    bestTarget = target;
                }
            }
            if (bestTarget == null)
            {
                MainTarget = null;
            }
            else if (MainTarget != bestTarget)
            {
                MainTarget = bestTarget;
                AudioPlayer.Stream = TargetBeep;
                AudioPlayer.Play();
                HomingIcon.Scale = Vector2.Zero;
                HomingOffscreenIcon.Scale = Vector2.Zero;
            }
        }
        else if (!IsHoming)
        {
            MainTarget = null;
        }
        else if (IsHoming)
        {
            homingAttackTimer += delta;
            ActionRoll.SetRoll(true);
            if (homingAttackTimer >= MaxHomingAttackTime)
            {
                IsHoming = false;
            }
        }

        var Camera = PlayerCamera.Instances[Player.PlayerID];

        //HomingIcon.Visible = (MainTarget != null && !Camera.IsPositionBehind(MainTarget.GlobalPosition));
        bool isIconOnScreen =
            MainTarget != null
            && !Camera.IsPositionBehind(MainTarget.GlobalPosition)
            && Camera.IsPositionInFrustum(MainTarget.GlobalPosition);
        HomingIcon.Visible = (isIconOnScreen);
        HomingOffscreenIcon.Visible = (MainTarget != null && !isIconOnScreen);

        HomingIcon.Scale = HomingIcon.Scale.Lerp(Vector2.One, (float)delta * 10);
        HomingOffscreenIcon.Scale = HomingIcon.Scale;

        HomingIcon.Rotate(Mathf.DegToRad(360 * (float)delta));
        if (MainTarget != null)
        {
            if (isIconOnScreen)
            {
                HomingIcon.GlobalPosition = Camera.UnprojectPosition(MainTarget.GlobalPosition);
            }
            else
            {
                var globalOffscreenPos = Camera.UnprojectPosition(MainTarget.GlobalPosition);
                var centerBasedPositionNormalized = (
                    Camera.GetViewport().GetVisibleRect().Size / 2 - globalOffscreenPos
                );
                //centerBasedPositionNormalized.X = Mathf.Clamp(centerBasedPositionNormalized.X, - Camera.GetViewport().GetVisibleRect().Size.X / 3, Camera.GetViewport().GetVisibleRect().Size.X / 3);
                //centerBasedPositionNormalized.Y = Mathf.Clamp(centerBasedPositionNormalized.Y, -Camera.GetViewport().GetVisibleRect().Size.Y / 3, Camera.GetViewport().GetVisibleRect().Size.Y / 3);
                centerBasedPositionNormalized = centerBasedPositionNormalized.Normalized();

                if (!Camera.IsPositionBehind(MainTarget.GlobalPosition))
                {
                    //if (centerBasedPositionNormalized.Y > 0)
                    //{
                    //	centerBasedPositionNormalized.Y = Camera.GetViewport().GetVisibleRect().Size.Y / 3;

                    //               }
                    //else
                    //{
                    //                   centerBasedPositionNormalized.Y = -Camera.GetViewport().GetVisibleRect().Size.Y / 3;
                    //               }
                    centerBasedPositionNormalized *= -1;
                }
                var camShortestDimension = Mathf.Min(
                    Camera.GetViewport().GetVisibleRect().Size.X,
                    Camera.GetViewport().GetVisibleRect().Size.Y
                );

                var edgePosition =
                    centerBasedPositionNormalized * camShortestDimension * 0.5f
                    + Camera.GetViewport().GetVisibleRect().Size / 2;
                HomingOffscreenIcon.GlobalPosition = edgePosition;

                HomingOffscreenIcon.LookAt(edgePosition - centerBasedPositionNormalized);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player.NpcPartnerControl.IsNpc)
            return;
        if (Player.PlayerDamage.IsDead)
        {
            IsHoming = false;
        }
        if (!IsInstanceValid(MainTarget) || !MainTarget.IsVisibleInTree())
        {
            MainTarget = null;
            IsHoming = false;
        }
        if (MainTarget != null && IsHoming)
        {
            Player.noGroundTimer = delta * 2;
            Player.LinearVelocity =
                (MainTarget.GlobalPosition - Player.GlobalPosition).Normalized()
                * Mathf.Max(HomingVelocity, Player.LinearVelocity.Length());
        }
    }

    private float GetTargetScore(Node3D target)
    {
        // TODO: Move input influence here
        return Player.GlobalPosition.DistanceTo(target.GlobalPosition);
    }

    public void StartHoming()
    {
        IsHoming = true;
        if (Player.Grounded)
        {
            ActionJump.Jump(true);
        }
        ActionJump.JumpCount = Math.Max(1, ActionJump.JumpCount);
        AudioPlayer.Stream = HomingSound;
        AudioPlayer.Play();
        if (
            !Vector3Utils
                .ProjectOnPlane(Player.GlobalPosition - MainTarget.GlobalPosition, Player.Basis.Y)
                .IsZeroApprox()
        )
        {
            Player.LookAt(MainTarget.GlobalPosition, Player.Basis.Y);
        }
        ActionRoll.SetRoll(true);
        ActionDash.airDashCount = 0;
        homingAttackTimer = 0;
        sonicGTHomingAttackSpeed = Player.LinearVelocity.Length();
        Player.LockVelocity(0, 0);
    }

    public void OnHomingTargetEnter(Node3D other)
    {
        var target = other.GetNodeOrNull<HomingTarget>("..");
        if (target != null)
        {
            Targets.Add(target);
        }
    }

    public void OnHomingTargetExit(Node3D other)
    {
        var target = other.GetNodeOrNull<HomingTarget>("..");
        if (target != null)
        {
            Targets.Remove(target);
        }
    }
}
