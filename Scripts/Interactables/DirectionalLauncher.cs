using System;
using Godot;

[Tool]
public partial class DirectionalLauncher : Node3D
{
    [Export]
    public Vector3 RelativeVelocityDir = Vector3.Forward;

    [Export]
    public float AddVelocity = 15;

    [Export]
    public float MinVelocity = 50;

    [Export]
    public float MaxVelocity = Mathf.Inf;

    [Export]
    public bool KeepPerpendicularVelocity = false;

    [Export]
    public float KeepVelocityDistance = 0;

    [Export]
    public float KeepVelocityTime = 0;

    [Export]
    public bool Grounded;

    [Export]
    public bool Rolling;

    [Export]
    public bool IsGrinding;

    [Export]
    public double LockInputTime;

    [Export]
    public bool Snap;

    [Export]
    public Vector3 RelativeSnapPoint = Vector3.Zero;

    [Export]
    public bool SnapCamera;

    [Export]
    public Vector3 SnapCameraRelativeDirection = Vector3.Forward;

    [Export]
    public Vector3 SnapCameraRelativeDirectionUp = Vector3.Up;

    [Export]
    public float ShakeCameraTime = 0.2f;

    [Export]
    public AudioStream SoundEffect;

    [Export]
    public string Animation;

    private float cooldown = 0.1f;
    private float lastUseTime = 0;

    public void OnTriggerEnter(Node3D other)
    {
        if (
            Time.GetTicksMsec()
            < lastUseTime + cooldown * 1000 /*s to ms conversion*/
        )
            return;

        var player = other.GetNodeOrNull<PlayerController>(new NodePath("."));
        if (player != null)
        {
            player.Grounded = Grounded || IsGrinding;
            player.PlayerInput.LeftInputTimedLock = LockInputTime;

            lastUseTime = Time.GetTicksMsec();

            if (Grounded)
            {
                player.noGroundTimer = 0;
            }

            if (!IsGrinding)
            {
                if (Grounded)
                {
                    if (player.PlayerInput.TwodimensionalMode && LockInputTime > 0)
                    {
                        player.PlayerInput.LeftRawInput = Vector2.Zero;
                        player.PlayerInput.LeftInput3D = Vector3.Zero;
                    }
                    else
                    {
                        player.PlayerInput.LeftInput3D =
                            this.GlobalTransform.Basis.GetRotationQuaternion().Normalized()
                            * this.RelativeVelocityDir.Normalized();
                    }
                    player.LookAt(
                        Vector3Utils.ProjectOnPlane(
                            this.GlobalPosition
                                + this.GlobalTransform.Basis.GetRotationQuaternion().Normalized()
                                    * RelativeVelocityDir,
                            this.Basis.Y
                        ),
                        this.Basis.Y
                    );
                }
                else
                {
                    player.PlayerInput.LeftInput3D = Vector3.Zero;
                    player.noGroundTimer = 0.1f;
                }
            }

            var dir =
                this.GlobalTransform.Basis.GetRotationQuaternion().Normalized()
                * RelativeVelocityDir.Normalized();
            if (!KeepPerpendicularVelocity)
            {
                var velocity = Mathf.Clamp(
                    player.LinearVelocity.Length() + AddVelocity,
                    MinVelocity,
                    MaxVelocity
                );
                player.LinearVelocity = dir * velocity;
            }
            else
            {
                var velocityXZ = player.LinearVelocity.ProjectOnPlane(dir);
                var velocityYDirected =
                    dir.Normalized()
                    * Mathf.Clamp(
                        Vector3Utils.LengthAlongsideVector(player.LinearVelocity, dir)
                            + AddVelocity,
                        MinVelocity,
                        MaxVelocity
                    );

                //Mathf.Clamp(player.LinearVelocity.Length() + AddVelocity, MinVelocity, MaxVelocity);
                player.LinearVelocity = velocityXZ + velocityYDirected;
            }

            if (Snap)
            {
                //player.MoveAndCollide(this.GlobalPosition + this.Basis.GetRotationQuaternion() * RelativeSnapPoint - player.GlobalPosition);
                player.GlobalPosition =
                    this.GlobalPosition + this.Basis.GetRotationQuaternion() * RelativeSnapPoint;
                if (Grounded)
                {
                    player.LookAt(
                        player.GlobalPosition + player.LinearVelocity.Normalized(),
                        this.GlobalBasis.Y
                    );
                    player.TimedLerpedRotation = LockInputTime;
                }
            }

            if (KeepVelocityDistance > 0 || KeepVelocityTime > 0)
            {
                KeepVelocity(player);
            }

            var rollAction = other.GetNode<ActionRoll>(
                new NodePath("./PlayerControl/Actions/ActionRoll")
            );
            if (rollAction != null)
            {
                rollAction.SetRoll(this.Rolling);
            }

            var homingAction = other.GetNode<ActionHoming>(
                new NodePath("./PlayerControl/Actions/ActionHoming")
            );
            if (homingAction != null)
            {
                homingAction.IsHoming = false;
            }

            var actionJump = other.GetNode<ActionJump>(
                new NodePath("./PlayerControl/Actions/ActionJump")
            );
            if (actionJump != null)
            {
                actionJump.JumpCount = 1;
            }

            var actionDash = other.GetNode<ActionDash>(
                new NodePath("./PlayerControl/Actions/ActionDash")
            );
            if (actionDash != null)
            {
                actionDash.airDashCount = 0;
            }

            var actionBoost = other.GetNodeOrNull<ActionBoost>(
                new NodePath("./PlayerControl/Actions/ActionBoost")
            );
            var actionGrind = other.GetNodeOrNull<ActionGrind>(
                new NodePath("./PlayerControl/Actions/ActionGrind")
            );
            if (
                actionBoost != null
                && !Grounded
                && (actionGrind == null || !actionGrind.IsGrinding())
            )
            {
                actionBoost.StopBoost();
            }

            var actionStomp = other.GetNodeOrNull<ActionStomp>(
                new NodePath("./PlayerControl/Actions/ActionStomp")
            );
            if (actionStomp != null)
            {
                actionStomp.CancelStomp();
            }

            var audioPlayer = other.GetNode<AudioStreamPlayer3D>(
                new NodePath("./Audio/InteractionsSoundEffectsPlayer")
            );
            if (audioPlayer != null)
            {
                audioPlayer.Stream = SoundEffect;
                audioPlayer.Play();
            }

            if (PlayerCamera.Instances.ContainsKey(player.PlayerID))
            {
                var camera = PlayerCamera.Instances[player.PlayerID];
                if (SnapCamera && camera.Mode == PlayerCamera.CameraMode.Free)
                {
                    var newUp = /*camera.VomitCamera ? this.GlobalBasis.GetRotationQuaternion().Normalized() * SnapCameraRelativeDirectionUp : */
                    camera.GlobalBasis.Y;
                    var newDir = (
                        this.GlobalBasis.GetRotationQuaternion().Normalized()
                        * SnapCameraRelativeDirection
                    )
                        .ProjectOnPlane(newUp)
                        .Normalized();

                    try
                    {
                        camera.LookAt(camera.GlobalPosition + newDir, newUp);
                    }
                    catch (Exception)
                    {
                        camera.LookAt(camera.GlobalPosition + newDir);
                    }
                    camera.StartLerp(0.2f);
                }
                if (ShakeCameraTime > 0)
                {
                    camera.SetShake(ShakeCameraTime);
                }
            }

            player.PlayerSkinController.TravelAnimation(Animation, true);

            EmitSignal(SignalName.OnLaunching);
        }
    }

    void KeepVelocity(PlayerController player)
    {
        player.LockVelocity(KeepVelocityDistance, KeepVelocityTime);
    }

    [Signal]
    public delegate void OnLaunchingEventHandler();
}
