using System;
using System.Collections.Generic;
using Godot;

public partial class PlayerCamera : Camera3D
{
    public enum CameraMode
    {
        Free, // Default camera. It follows the player and you can rotate it freely
        Constant, // Follows the player, but keeps looking into the same direction.
        Spline3D, // Follows the player. Looks at the nearest direction according to Spline, but also can move up/down/left/right. Something like in most boost games.
        Spline3DStrict, // Is positioned on the spline and looks at the direction according to the Spline.
        Spline2D, // Follows the player from the side. Side is defined by the nearest point on Spline.
        Point, // Copies transform from Point
        LookAtPoint, // Similar to Free camera, but instead of leting player control it manually, it looks at Point.
        LookFromPoint, // Camera is positioned at Point and looks at player
        Locked, // Camera stays where it is and does nothing. No movement, no rotation.
    }

    public int PlayerID = 0;
    public PlayerController Player;
    public List<Node3D> CameraTargets = new List<Node3D>();

    [Export]
    public float MaxCameraDistance = 4;

    [Export]
    public bool DisableRaycast = false;

    [Export]
    public float CameraHitObstacleRepositionOffset = 0.3f;

    [Export]
    public float GroundNormalRepositionSpeed = 16;

    [Export]
    public float UpRepositionSpeed = 4;

    [Export]
    public float ForwardRepositionSpeed = 2;

    [Export]
    public float ConstantDirectionRepositionSpeed = 8;

    [Export]
    public bool ConstantDirectionModifiedBySpeed = false;

    [Export]
    public float StableGroundDot = 0.95f;

    [Export]
    public Vector2 CameraSensitivity = new Vector2(10, 0.5f);

    [Export]
    public Vector2 CameraLimitsY = new Vector2(-0.0f, 0.5f);

    [Export]
    public Quaternion ConstantCameraDirection = Quaternion.Identity;

    [Export(PropertyHint.Range, "0.0,1.0")]
    public float ConstantCameraStrength = 90;
    public Path3D Spline;
    public Node3D Point;

    [Export]
    public CameraMode Mode;

    [Export]
    public Vector3 CameraOffset;

    [Export]
    public bool CardinalDirectionBias = false;

    [Export]
    public float CardinalDirectionMinDot = 0.75f;

    [Export]
    public bool SettingAutoAirCamera = false;

    [Export]
    public bool SettingEasing = true;

    [Export]
    public float EasingSpeed = 20f;

    [Export]
    public bool VomitCamera = true;

    [Export]
    public float NonVomitStabilizationDot = 0.9f;

    [Export]
    public bool DynamicFov = true;

    [Export]
    public bool AutoLookDown = true;

    [Export]
    public bool AnimeSpeedLines = true;

    [Export]
    public ColorRect AnimeSpeedLinesRect;

    [Export]
    public Filters Filters;
    public float WaitTime = 0;

    private float cameraDistance;
    private Vector3 CameraAverage;
    private float UpDownManualCorrection = 0;
    private Vector3 CollissionOffset = Vector3.Zero;
    private double CameraNotCollidedTimer = 0;

    private Vector3 CurrentShake = Vector3.Zero;
    private float ShakeTime = 0;
    private float ShakeSpeed = 10;

    [Export]
    public float ShakeStrength = 2f;
    private float ShakeCurrentStrength;
    private float ShakeDegradationSpeed;

    // For transition between states
    private Transform3D LerpStartPosition;
    private float LerpTime = 0;
    private float LerpTimer = 0;

    private Vector3 playerUp = Vector3.Up;

    public static Godot.Collections.Dictionary<int, PlayerCamera> Instances =
        new Godot.Collections.Dictionary<int, PlayerCamera>(); // Key: Player ID

    public override void _Ready()
    {
        cameraDistance = MaxCameraDistance;

        Instances.Add(PlayerID, this);

        OptionsMenu.UpdatePlayerCameraSettings();
    }

    public override void _ExitTree()
    {
        if (Player != null)
        {
            Instances.Remove(PlayerID);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player == null)
        {
            Player = PlayerController.Instances[PlayerID];
            if (!CameraTargets.Contains(Player))
            {
                CameraTargets.Add(Player);
            }
        }

        if (DynamicFov)
        {
            SetFov(delta);
        }

        SetAnimeSpeedLines(delta);

        if (Player.PlayerDamage.IsDead)
            return;

        if (Mode == CameraMode.Locked || delta == 0)
            return;
        else if (Mode == CameraMode.Free)
        {
            FreeCamera(delta, false);
        }
        else if (Mode == CameraMode.Constant)
        {
            FreeCamera(delta, true);
        }
        else if (Mode == CameraMode.Spline3D || Mode == CameraMode.Spline2D)
        {
            SplineCamera(delta);
        }
        else if (Mode == CameraMode.Spline3DStrict)
        {
            StrictSplineCamera(delta);
        }
        else if (Mode == CameraMode.Point)
        {
            PointCamera(delta);
        }
        else if (Mode == CameraMode.LookAtPoint)
        {
            FreeCamera(delta, false, Point.GlobalPosition);
        }
        else if (Mode == CameraMode.LookFromPoint)
        {
            LookFromPointCamera(delta);
        }

        ApplyLerp(delta);
    }

    public void FreeCamera(double delta, bool useConstantDirection, Vector3? lookAtPoint = null)
    {
        // CameraAverage is basically a target to look at. If there are multiple specified targets (for example - in multiplayer or during boss fights), then the camera will look between them. Adjust MaxCameraDistance accordingly!
        CameraAverage = Vector3.Zero;

        foreach (var t in CameraTargets)
        {
            CameraAverage += t.GlobalPosition;
        }
        CameraAverage /= (CameraTargets.Count);

        var target = CameraAverage;

        Vector3 newPlayerUp =
            Player.Grounded
            && Player.GroundNormal.Normalized().Dot(-Player.Gravity.Normalized()) >= StableGroundDot
                ? -Player.Gravity.Normalized()
                : Player.GroundNormal.Normalized();
        if (playerUp.Dot(newPlayerUp) < 0)
        {
            // Snap - it happens when player jumps off the ceiling and then quickly lands. We need to prevent clipping in such case
            playerUp = newPlayerUp;
        }
        else
        {
            playerUp = playerUp
                .Lerp(newPlayerUp, Mathf.Clamp((float)delta * GroundNormalRepositionSpeed, 0, 1))
                .Normalized();
        }
        if (playerUp.IsZeroApprox())
        {
            playerUp = newPlayerUp;
        }

        if (!VomitCamera && !useConstantDirection)
        {
            playerUp = -Player.Gravity.Normalized();
        }

        // If camera is non-vomit, then don't rotate when on walls.
        bool stabilizeNonVomitCamera =
            !VomitCamera
            && (
                Player.Grounded
                && Player.GroundNormal.Normalized().Dot(-Player.Gravity.Normalized())
                    < NonVomitStabilizationDot
            ); // TODO BUG: It breaks during gravity change
        stabilizeNonVomitCamera = false;

        var CurrentCameraOffset = CameraOffset;

        // Shake
        if (ShakeTime > 0)
        {
            ShakeTime -= (float)delta;
            var nextShake = new Vector3(
                (float)GD.RandRange(-ShakeCurrentStrength, ShakeCurrentStrength),
                (float)GD.RandRange(-ShakeCurrentStrength, ShakeCurrentStrength),
                0
            );
            ShakeCurrentStrength -= ShakeDegradationSpeed * (float)delta;
            CurrentShake = CurrentShake.Lerp(nextShake, (float)delta * ShakeSpeed);
            CurrentCameraOffset += CurrentShake;
        }

        uint collissionMask = 1;
        //if (!DisableRaycast)
        //{
        //    var rayQueryCamOffset = PhysicsRayQueryParameters3D.Create(target + this.GlobalBasis.Y * 0.1f, target + this.GlobalBasis.Y * 0.1f + CameraOffset, collissionMask);
        //    var rayCamOffsetResult = GetWorld3D().DirectSpaceState.IntersectRay(rayQueryCamOffset);
        //    if (rayCamOffsetResult.Count > 0)
        //    {
        //        var hitPoint = rayCamOffsetResult["position"].AsVector3();
        //        var hitOffset = target + this.GlobalBasis.Y * 0.1f + this.GlobalBasis.Y * 0.1f - hitPoint;
        //        CurrentCameraOffset = -hitOffset;
        //    }
        //}

        if (useConstantDirection || stabilizeNonVomitCamera)
        {
            Vector3 constantUp = -Player.Gravity;
            Quaternion playerRotation;
            if (useConstantDirection)
            {
                ConstantCameraDirection = ConstantCameraDirection.Normalized();
                constantUp = ConstantCameraDirection * Vector3.Up;
                CurrentCameraOffset = ConstantCameraDirection * CurrentCameraOffset;
            }
            else if (stabilizeNonVomitCamera)
            {
                //constantUp = this.GlobalBasis.Y;
                constantUp = -this.Player.Gravity.Normalized();
            }
            playerRotation = new Quaternion(constantUp, playerUp).Normalized();

            if (constantUp.Normalized().Dot(-Player.GroundNormal) >= 0.99f)
            {
                target += -CurrentCameraOffset + CollissionOffset; // This prevents glitching when player runs on flat ceiling.
            }
            else
            {
                target += playerUp.Normalized() * CurrentCameraOffset.Length(); // playerRotation * (CurrentCameraOffset + CollissionOffset);
            }
        }
        else
        {
            if (VomitCamera)
            {
                target +=
                    this.GlobalBasis.GetRotationQuaternion()
                    * (CurrentCameraOffset + CollissionOffset);
            }
            else
            {
                target +=
                    Player.GlobalBasis.GetRotationQuaternion()
                    * (CurrentCameraOffset + CollissionOffset);
            }
        }

        float MaxCameraDistanceFovAdjusted = MaxCameraDistance;
        if (DynamicFov)
        {
            if (
                Vector3Utils.LengthAlongsideVector(
                    Player.VelocityXZ / Player.SpeedMultiplier,
                    -this.GlobalBasis.Z
                ) > 0
            )
            {
                var fovFactor = Mathf.Clamp((this.Fov - 75) / (120 - 75), 0, 1);
                MaxCameraDistanceFovAdjusted =
                    MaxCameraDistance
                    - (
                        Mathf.Clamp(fovFactor, 0, 1)
                        * Mathf.Clamp(fovFactor, 0, 1)
                        * (MaxCameraDistance * (SettingEasing ? 1f : 0.5f))
                    );
            }
        }

        // Raycast to detect wall hits. Shorten cameraDistance if necessary.

        bool dontZoomIn = false;
        if (CameraTargets.Count > 1)
        {
            foreach (var t in CameraTargets)
            {
                if (
                    !this.IsPositionInFrustum(
                        t.GlobalPosition + this.GlobalBasis.Z * (MaxCameraDistanceFovAdjusted + 1)
                    )
                )
                {
                    dontZoomIn = true;
                }

                if (!this.IsPositionInFrustum(t.GlobalPosition))
                {
                    dontZoomIn = true;
                }

                if (
                    !this.IsPositionInFrustum(
                        t.GlobalPosition + this.GlobalBasis.Z * MaxCameraDistanceFovAdjusted
                    )
                )
                {
                    cameraDistance += 5f * (float)delta;
                    dontZoomIn = true;
                    break;
                }
            }
        }

        if (!dontZoomIn)
        {
            if (cameraDistance < MaxCameraDistanceFovAdjusted)
            {
                cameraDistance = Mathf.Lerp(
                    cameraDistance,
                    MaxCameraDistanceFovAdjusted,
                    Mathf.Clamp((float)delta * 10f, 0, 1)
                );
            }
            else
            {
                cameraDistance = Mathf.Lerp(
                    cameraDistance,
                    MaxCameraDistanceFovAdjusted,
                    Mathf.Clamp((float)delta, 0, 1)
                );
            }
        }

        bool cameraCollided = false;
        Vector3 sideHitNormal = Vector3.Zero;
        if (!DisableRaycast)
        {
            // Is "target" behind wall? Put it closer.
            //{
            //    var rayQueryForward = PhysicsRayQueryParameters3D.Create(this.GlobalPosition, CameraAverage + this.GlobalBasis.GetRotationQuaternion() * (CurrentCameraOffset + CollissionOffset), collissionMask);
            //    rayQueryForward.HitBackFaces = false;
            //    var rayForwardResult = GetWorld3D().DirectSpaceState.IntersectRay(rayQueryForward);
            //    if (rayForwardResult.Count > 0)
            //    {
            //        var hitPoint = rayForwardResult["position"].AsVector3();
            //        target = CameraAverage + this.GlobalBasis.GetRotationQuaternion() * (CurrentCameraOffset + CollissionOffset);
            //        CollissionOffset = Vector3.Zero;
            //        cameraCollided = true;
            //    }
            //}

            // Forward ray
            {
                var dir = this.GlobalBasis.Z * cameraDistance;
                var rayQueryForward = PhysicsRayQueryParameters3D.Create(
                    target,
                    target + dir,
                    collissionMask
                );

                var rayForwardResult = GetWorld3D().DirectSpaceState.IntersectRay(rayQueryForward);
                if (rayForwardResult.Count > 0)
                {
                    var hitPoint = rayForwardResult["position"].AsVector3();
                    var hitNormal = rayForwardResult["normal"].AsVector3();
                    var hitOffset = target - hitPoint;
                    var hitOffsetSide =
                        Vector3Utils.Project(target, playerUp)
                        - Vector3Utils.Project(hitPoint, playerUp);
                    cameraDistance = hitOffset.Length() - this.Near * 2;
                    //CollissionOffset = Vector3Utils.Project(hitNormal, playerUp).Normalized() * (hitOffsetSide.Length() + this.Near * 2);

                    cameraCollided = true;
                    useConstantDirection = false;
                }
                else
                {
                    CollissionOffset = Vector3.Zero;
                }
            }
            //CollissionOffset = Vector3.Zero;
            //if (!cameraCollided /*&& OS.GetName() != "Android" && OS.GetName() != "iOS"*/)
            {
                // Sides ray
                foreach (
                    var dir in new Vector3[]
                    {
                        //this.GlobalBasis.Y * 1,
                        //-this.GlobalBasis.Y * 1,
                        //this.GlobalBasis.X * 1,
                        //-this.GlobalBasis.X * 1,

                        //new Vector3(-this.Size, 0,0),
                        //new Vector3(this.Size, 0, 0),
                        //new Vector3(0, this.Size, 0),
                        //new Vector3(0, -this.Size, 0),

                        this.ProjectPosition(
                            new Vector2(0, this.GetViewport().GetVisibleRect().GetCenter().Y),
                            -this.Near
                        ) - this.GlobalPosition,
                        this.ProjectPosition(
                            new Vector2(
                                this.GetViewport().GetVisibleRect().Size.X,
                                this.GetViewport().GetVisibleRect().GetCenter().Y
                            ),
                            -this.Near
                        ) - this.GlobalPosition,
                        this.ProjectPosition(
                            new Vector2(this.GetViewport().GetVisibleRect().GetCenter().X, 0),
                            -this.Near
                        ) - this.GlobalPosition,
                        this.ProjectPosition(
                            new Vector2(
                                this.GetViewport().GetVisibleRect().GetCenter().X,
                                this.GetViewport().GetVisibleRect().Size.Y
                            ),
                            -this.Near
                        ) - this.GlobalPosition,
                        //this.GlobalPosition - (Player.GlobalPosition + Player.Basis.Y * 0.5f)
                    }
                )
                {
                    // var rayQueryForward = PhysicsRayQueryParameters3D.Create(target, target + dir, collissionMask);
                    var projectedCollissionOffset = CollissionOffset.Project(dir);

                    var rayQueryForward = PhysicsRayQueryParameters3D.Create(
                        this.GlobalPosition - projectedCollissionOffset,
                        this.GlobalPosition + dir * 2 - projectedCollissionOffset,
                        collissionMask
                    );
                    rayQueryForward.HitBackFaces = false;
                    var rayForwardResult = GetWorld3D()
                        .DirectSpaceState.IntersectRay(rayQueryForward);
                    if (rayForwardResult.Count > 0)
                    {
                        if (!cameraCollided)
                        {
                            CollissionOffset = Vector3.Zero;
                        }

                        var hitPoint = rayForwardResult["position"].AsVector3();
                        var hitNormal = rayForwardResult["normal"].AsVector3();
                        var hitOffset =
                            (this.GlobalPosition - projectedCollissionOffset) - hitPoint;

                        CollissionOffset += hitNormal.Normalized() * dir.Length();

                        sideHitNormal = hitNormal;
                        cameraCollided = true;
                        useConstantDirection = false;
                        CameraNotCollidedTimer = 0;
                        break;
                    }
                }
            }
        }

        if (!cameraCollided)
        {
            const double COLLISSION_WAIT_TIME = 0.1f;
            if (CameraNotCollidedTimer > COLLISSION_WAIT_TIME)
            {
                CollissionOffset = CollissionOffset.Lerp(
                    Vector3.Zero,
                    (float)delta
                        * (float)Mathf.Clamp(CameraNotCollidedTimer - COLLISSION_WAIT_TIME, 0, 1f)
                );
            }
            CameraNotCollidedTimer += delta;
        }

        // Reposition & rotation
        Quaternion upRotation = new Quaternion(this.GlobalBasis.Y, playerUp).Normalized();
        var up = this.GlobalBasis.Y;
        if (this.GlobalBasis.Y.Dot(playerUp.Normalized()) < -0.99f)
        {
            up = playerUp; // Special case: 180 degree rotation needs to be rotated around Z axis to not flip camera backwards
        }
        else if (
            this.GlobalBasis.Y.Dot(playerUp.Normalized()) > 0.9999f
            && this.GlobalBasis.Y.Dot(-Player.Gravity.Normalized()) > 0.9999f
        )
        {
            up = playerUp;
        }
        else
        {
            up =
                Quaternion
                    .Identity.Slerpni(
                        upRotation,
                        Mathf.Clamp((float)delta * UpRepositionSpeed, 0, 1)
                    )
                    .Normalized() * this.GlobalBasis.Y;
            //up = (upRotation * ((float)delta/10)).Normalized() * this.GlobalBasis.Y;
        }

        //var up = this.GlobalBasis.Y.Lerp(playerUp, Mathf.Clamp((float)delta * UpRepositionSpeed,0,1));

        //var relativePosition = -Vector3Utils.ProjectOnPlane((-this.GlobalBasis.Z) * cameraDistance, Player.GroundNormal);
        //var relativeTarget = Vector3Utils.ProjectOnPlane(CameraOffset, Player.GroundNormal);

        var currentRelativePosition = -(-this.GlobalBasis.Z) * cameraDistance;
        var playerVelocity = Player.VelocityXZ;
        if (playerVelocity.Length() < 1 || stabilizeNonVomitCamera)
        {
            playerVelocity = Vector3.Zero;
        }

        var relativePosition = currentRelativePosition;
        var velocityPositionOffset = Vector3.Zero;
        if (playerVelocity.Length() > 0)
        {
            velocityPositionOffset = -playerVelocity.Normalized() * cameraDistance;
            relativePosition = velocityPositionOffset;
        }

        if (stabilizeNonVomitCamera || (!SettingAutoAirCamera && !Player.Grounded))
        {
            relativePosition = currentRelativePosition;
        }

        //var inputOffset = this.GlobalBasis.GetRotationQuaternion() * new Vector3(-PlayerInput.CameraInput2D.X * CameraSensitivity.X * (float)delta, PlayerInput.CameraInput2D.Y * CameraSensitivity.Y * (float)delta, 0);
        var inputOffset = UpDownManualControls(delta);
        if (useConstantDirection)
        {
            //            relativePosition = relativePosition.Lerp(ConstantCameraDirection.Normalized() * Vector3.Forward * cameraDistance, Mathf.Clamp(ConstantCameraStrength * (float)delta, 0,1));
            if (ConstantDirectionModifiedBySpeed)
            {
                relativePosition =
                    ConstantCameraDirection.Normalized() * Vector3.Forward * cameraDistance
                    + velocityPositionOffset;
            }
            else
            {
                relativePosition =
                    ConstantCameraDirection.Normalized() * Vector3.Forward * cameraDistance;
            }

            up = ConstantCameraDirection.Normalized() * Vector3.Up;
            inputOffset = Vector3.Zero;
        }
        else if (stabilizeNonVomitCamera)
        {
            relativePosition = -this.GlobalBasis.Z * cameraDistance;
            up = this.GlobalBasis.Y;
        }

        //if (lookAtPoint != null)
        //{
        //    relativePosition = Vector3Utils.ProjectOnPlane((this.GlobalPosition - lookAtPoint.Value).Normalized(), up.Normalized()).Normalized() * cameraDistance;
        //}

        if (
            (this.GlobalBasis.Z).Dot(relativePosition.Normalized()) < -0.9f
            && !useConstantDirection
        )
        {
            relativePosition = relativePosition.Reflect(this.GlobalBasis.Z);
        }

        //relativePosition = Vector3Utils.ProjectOnPlane(relativePosition, up);

        //LookAtFromPosition(target + Vector3Utils.ProjectOnPlane(currentRelativePosition.Lerp(relativePosition, (float)delta*ForwardRepositionSpeed), up) + inputOffset, target, up);
        var newTransform = this.GlobalTransform;

        var newRelativePositionRepositionedXZ = currentRelativePosition
            .ProjectOnPlane(up)
            .Lerp(
                relativePosition.ProjectOnPlane(up),
                Mathf.Clamp((float)delta * ForwardRepositionSpeed, 0, 1)
            );
        var newRelativePositionRepositionedY = currentRelativePosition
            .Project(up)
            .Lerp(
                relativePosition.Project(up),
                Mathf.Clamp((float)delta * UpRepositionSpeed, 0, 1)
            );
        var newRelativePositionRepositioned =
            newRelativePositionRepositionedXZ + newRelativePositionRepositionedY;

        //if (up.Dot(Player.GroundNormal) < 0.5f)    // Too fast rotation, screw fluidity, we need camera to not freak out.
        //{
        //    newRelativePositionRepositioned = relativePosition.ProjectOnPlane(up);
        //}

        //var upDifference = new Quaternion(this.GlobalBasis.Y.ProjectOnPlane(this.GlobalBasis.X).Normalized(), up.ProjectOnPlane(this.GlobalBasis.X).Normalized()).Normalized();
        //newRelativePositionRepositioned *= upDifference;

        var newPosition = target + newRelativePositionRepositioned;
        // var newPosition = target + currentRelativePosition.Lerp(relativePosition, Mathf.Clamp((float)delta * ForwardRepositionSpeed, 0, 1));

        newTransform.Origin = newPosition;

        //      if (Player.PlayerInput.CameraInput2D.Length() > 0.25f)
        //{
        //	newTransform.Origin = newPosition;
        //      }
        //else if (newTransform.Origin.DistanceTo(newPosition) < 0.25f) {   // Microshake reduction
        //          newTransform.Origin = newTransform.Origin.Lerp(newPosition, (float)delta);
        //      } else
        //{
        //          //newTransform.Origin = newPosition;
        //          newTransform.Origin = newTransform.Origin.Lerp(newPosition, (float)delta * 10);
        //      }

        //if (newTransform.Origin.DistanceTo(newPosition) > 0.1f && Player.PlayerInput.CameraInput2D.Length() < 0.5f)
        //{   // Microshake reduction. As a side effect it makes zoom in/out during acceleration so I'm not bothering to fix it.
        //    newTransform.Origin = newTransform.Origin.Lerp(newPosition, (float)delta * 10);
        //}
        //else
        //{
        //    newTransform.Origin = newPosition;

        //}
        var targetOffset = target - newTransform.Origin;

        if (CardinalDirectionBias)
        {
            var xdot = targetOffset.Normalized().Dot(Vector3.Right);
            var zdot = targetOffset.Normalized().Dot(Vector3.Forward);
            if (Mathf.Abs(xdot) > CardinalDirectionMinDot)
            {
                targetOffset =
                    targetOffset
                        .Lerp(
                            targetOffset.Project(Vector3.Right),
                            ForwardRepositionSpeed * (float)delta
                        )
                        .Normalized() * targetOffset.Length();
            }
            if (Mathf.Abs(zdot) > CardinalDirectionMinDot)
            {
                targetOffset =
                    targetOffset
                        .Lerp(
                            targetOffset.Project(Vector3.Forward),
                            ForwardRepositionSpeed * (float)delta
                        )
                        .Normalized() * targetOffset.Length();
            }
        }

        var lookOffset = targetOffset.ProjectOnPlane(up) - inputOffset + -newTransform.Basis.Z;
        if (cameraCollided && !sideHitNormal.IsZeroApprox())
        {
            if (lookOffset.Normalized().Dot(sideHitNormal) > 0.5f)
            {
                lookOffset =
                    lookOffset
                        .Normalized()
                        .Lerp(
                            lookOffset.Project(sideHitNormal).Normalized(),
                            (float)delta * ForwardRepositionSpeed
                        )
                        .Normalized() * lookOffset.Length();
            }
            else
            {
                lookOffset =
                    lookOffset
                        .Normalized()
                        .Lerp(
                            lookOffset.ProjectOnPlane(sideHitNormal).Normalized(),
                            (float)delta * ForwardRepositionSpeed
                        )
                        .Normalized()
                    * lookOffset.Length()
                    * lookOffset.Length();
            }
        }

        if (lookAtPoint != null)
        {
            //lookOffset = Vector3Utils.ProjectOnPlane((lookAtPoint.Value - this.GlobalPosition).Normalized(), up.Normalized()).Normalized() * cameraDistance;
            lookOffset = (lookAtPoint.Value - this.GlobalPosition).Normalized() * cameraDistance;
        }

        if (lookOffset.Length() == 0)
        {
            lookOffset = -newTransform.Basis.Z;
        }
        newTransform = newTransform.LookingAt(newTransform.Origin + lookOffset, up);

        var lerpedNewTransform = newTransform;
        //lerpedNewTransform.Basis = newTransform.Basis.Orthonormalized().Slerp(this.GlobalTransform.Basis.Orthonormalized(), (float)delta * 10).Orthonormalized();
        if (WaitTime > 0)
        {
            WaitTime -= (float)delta;
            lerpedNewTransform.Origin = this.GlobalTransform.Origin;
        }
        if (!Player.PlayerInput.TwodimensionalMode && SettingEasing)
        {
            lerpedNewTransform.Origin = this.GlobalTransform.Origin.Lerp(
                newTransform.Origin,
                Mathf.Clamp((float)delta * EasingSpeed, 0, 1)
            );
            if (Player.PlayerInput.CameraInput2D.Length() < 0.1f)
            {
                //lerpedNewTransform.Basis = this.GlobalTransform.Basis.Orthonormalized().Slerp(newTransform.Basis.Orthonormalized(), Mathf.Clamp((float)delta * ProjectHeroEasingSpeed,0,1)).Orthonormalized();
            }
        }
        if (stabilizeNonVomitCamera)
        {
            this.GlobalTransform = newTransform;
        }
        else
        {
            this.GlobalTransform = lerpedNewTransform;
        }
    }

    public Vector3 UpDownManualControls(double delta)
    {
        // Controls X
        var inputOffset =
            this.GlobalBasis.GetRotationQuaternion()
            * new Vector3(
                -Player.PlayerInput.CameraInput2D.X * CameraSensitivity.X * (float)delta,
                0,
                0
            );
        //inputOffset = inputOffset.ProjectOnPlane(Player.GroundNormal);

        // Controls Y
        if (Mathf.Abs(Player.PlayerInput.CameraInput2D.Y) > 0.5f)
        {
            UpDownManualCorrection = Mathf.Clamp(
                UpDownManualCorrection
                    + Player.PlayerInput.CameraInput2D.Y * CameraSensitivity.Y * (float)delta,
                CameraLimitsY.X,
                CameraLimitsY.Y
            );
        }
        else if (AutoLookDown && Player.VelocityYDirected < -20 && !Player.Grounded)
        {
            UpDownManualCorrection = Mathf.Lerp(
                UpDownManualCorrection,
                CameraLimitsY.Y,
                (float)delta * 0.5f
            );
        }
        else
        {
            UpDownManualCorrection = Mathf.Lerp(UpDownManualCorrection, 0, (float)delta);
        }
        inputOffset += Player.GroundNormal.Normalized() * UpDownManualCorrection;
        return inputOffset;
    }

    public void SplineCamera(double delta)
    {
        var nearestOffset = Spline.Curve.GetClosestOffset(
            Spline.GlobalTransform.AffineInverse() * Player.GlobalPosition
        );
        var nearestPoint =
            Spline.GlobalTransform * Spline.Curve.SampleBakedWithRotation(nearestOffset);

        switch (Mode)
        {
            case CameraMode.Spline3D:
                ConstantCameraDirection = nearestPoint.Basis.GetRotationQuaternion();
                break;
            case CameraMode.Spline2D:
                ConstantCameraDirection =
                    nearestPoint.Basis.GetRotationQuaternion()
                    * new Quaternion(Vector3.Forward, Vector3.Right);
                break;
        }

        FreeCamera(delta, true);
    }

    public void StrictSplineCamera(double delta)
    {
        //FreeCamera(delta, true);

        var nearestOffset = Spline.Curve.GetClosestOffset(
            Spline.GlobalTransform.AffineInverse() * Player.GlobalPosition
        );
        var nearestPoint =
            Spline.GlobalTransform
            * Spline.Curve.SampleBakedWithRotation(nearestOffset - this.MaxCameraDistance);

        var offset = this.GlobalTransform.Basis.GetRotationQuaternion() * CameraOffset;
        nearestPoint.Origin += offset;

        //nearestPoint.Origin = this.GlobalTransform.Origin.Lerp(nearestPoint.Origin, (float)delta * ForwardRepositionSpeed);
        //nearestPoint.Basis = new Basis(this.GlobalTransform.Basis.GetRotationQuaternion().Normalized().Slerp(nearestPoint.Basis.GetRotationQuaternion().Normalized(), (float)delta * ForwardRepositionSpeed));

        this.GlobalTransform = nearestPoint;
    }

    public void PointCamera(double delta)
    {
        this.GlobalTransform = Point.GlobalTransform;
    }

    public void LookFromPointCamera(double delta)
    {
        this.LookAtFromPosition(Point.GlobalPosition, Player.GlobalPosition);
    }

    public void ConstantCamera(double delta) { }

    void SetFov(double delta)
    {
        //if (!Player.Grounded) return;
        float speed = 0;
        if (Player != null && !Player.PlayerDamage.IsDead && !StageData.Instance.IsFinished)
        {
            speed =
                Vector3Utils.Project(Player.VelocityXZ, this.GlobalBasis.Z).Length()
                / Player.SpeedMultiplier;
        }
        if (speed < 50)
            speed = 50;
        var speedFov = Mathf.Lerp(
            60,
            120,
            Mathf.Clamp(Mathf.Pow((speed - 50) / (100 - 50), 2), 0, 1)
        );
        this.Fov = Mathf.Lerp(this.Fov, speedFov, (float)delta * 10);
    }

    void SetAnimeSpeedLines(double delta)
    {
        float speed = 0;
        if (
            AnimeSpeedLines
            && Player != null
            && !Player.PlayerDamage.IsDead
            && !StageData.Instance.IsFinished
        )
        {
            speed =
                Vector3Utils.Project(Player.VelocityXZ, this.GlobalBasis.Z).Length()
                / Player.SpeedMultiplier;
        }
        var material = AnimeSpeedLinesRect.Material;

        const float MIN_ANIME_LINES_SPEED = 50f;

        var opacity = Mathf.Clamp(
            (speed - MIN_ANIME_LINES_SPEED) / (100f - MIN_ANIME_LINES_SPEED),
            0,
            1
        );
        material.Set("shader_parameter/Opacity", opacity);
        var centerScale =
            1f
            - 0.1f
                * Mathf.Clamp(
                    (speed - MIN_ANIME_LINES_SPEED) / (100f - MIN_ANIME_LINES_SPEED),
                    0,
                    1
                );
        material.Set("shader_parameter/CenterScale", centerScale);
    }

    public void SetPlayer(int newPlayerID)
    {
        if (Player != null)
        {
            CameraTargets.Remove(Player);
        }
        PlayerID = newPlayerID;
        GD.Print("New player ID ", newPlayerID);
        Player = PlayerController.Instances[PlayerID];
        if (!CameraTargets.Contains(Player))
        {
            GD.Print("Added target ", Player.PlayerID);
            CameraTargets.Add(Player);
        }
    }

    public void SetShake(float time = 0.5f)
    {
        ShakeTime = time;
        ShakeCurrentStrength = ShakeStrength;
        ShakeDegradationSpeed = ShakeStrength / time;
    }

    public void StartLerp(float time)
    {
        LerpStartPosition = this.GlobalTransform;
        LerpTimer = 0;
        LerpTime = time;
    }

    public void ApplyPortalWarp(Basis warpBasis)
    {
        playerUp = (warpBasis * playerUp).Normalized();
        CollissionOffset = warpBasis * CollissionOffset;
        CameraNotCollidedTimer = 0;
        if (LerpTime > 0 && LerpTimer < LerpTime)
        {
            LerpStartPosition = new Transform3D(
                warpBasis * LerpStartPosition.Basis,
                warpBasis * LerpStartPosition.Origin
            );
        }
    }

    void ApplyLerp(double delta)
    {
        if (LerpTime > 0 && LerpTimer < LerpTime)
        {
            var lerpedTransform = LerpStartPosition;
            lerpedTransform.Origin = LerpStartPosition.Origin.Lerp(
                this.GlobalPosition,
                LerpTimer / LerpTime
            );
            lerpedTransform.Basis = new Basis(
                LerpStartPosition
                    .Basis.GetRotationQuaternion()
                    .Normalized()
                    .Slerp(
                        this.GlobalTransform.Basis.GetRotationQuaternion().Normalized(),
                        LerpTimer / LerpTime
                    )
            );

            this.GlobalTransform = lerpedTransform;

            LerpTimer += (float)delta;
            if (LerpTimer >= LerpTime)
            {
                LerpTime = 0;
            }
        }
    }
}
