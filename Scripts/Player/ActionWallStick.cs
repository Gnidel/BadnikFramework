using Godot;

public partial class ActionWallStick : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionRoll ActionRoll;

    [Export]
    public GpuParticles3D WallSlideDustParticles;

    private ActionJump ActionJump;
    private ActionHoming ActionHoming;

    [Export]
    public float SlideSpeed = 3f;

    [Export]
    public float WallRunDistance = 0f;

    [Export]
    public float WallSlideDistance = 0.5f;

    [Export]
    public bool AllowAnyWall;

    [Export]
    public bool UseWallRunAnimations;

    [Export]
    public float WallRunSpeed = 10f;

    [Export]
    public bool PreserveInitialMomentum = true;

    [Export]
    public float WallJumpVelocity = 45f;

    [Export]
    public double WallJumpInputLockTime = 0.2;

    public bool IsWallSticking { get; private set; }
    public string CurrentWallAnimation { get; private set; } = "WallStick";
    private Vector3 wallNormal;
    private Vector3 wallMomentum;
    private double anyWallStickCooldown;

    public override void _Ready()
    {
        if (Player == null)
            Player = GetNodeOrNull<PlayerController>("../../..");
        if (ActionRoll == null)
            ActionRoll = Player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        ActionJump = Player.GetNodeOrNull<ActionJump>("./PlayerControl/Actions/ActionJump");
        ActionHoming = Player.GetNodeOrNull<ActionHoming>("./PlayerControl/Actions/ActionHoming");
        if (WallSlideDustParticles?.ProcessMaterial != null)
        {
            WallSlideDustParticles.ProcessMaterial =
                WallSlideDustParticles.ProcessMaterial.Duplicate() as ParticleProcessMaterial;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (
            IsWallSticking
            && Player.PlayerInput.IsActionPressed(@event, ActionJump.JUMP_INPUT_NAME)
        )
        {
            WallJump();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        anyWallStickCooldown = Mathf.Max((float)(anyWallStickCooldown - delta), 0f);
        if (
            !IsWallSticking
            && AllowAnyWall
            && anyWallStickCooldown <= 0
            && !Player.Grounded
            && CanBeginWallStick()
        )
        {
            if (TryGetAnyWallContactPoint(out var anyWallPoint, out var anyWallNormal))
                BeginWallStick(anyWallNormal, anyWallPoint);
        }

        if (!IsWallSticking)
            return;

        if (Player.Grounded)
        {
            EndWallStick();
            return;
        }

        var foundWallContact = false;
        Vector3 wallContactPoint;
        Vector3 contactNormal;
        if (TryGetFrontWallContactPoint(out wallContactPoint, out contactNormal))
        {
            foundWallContact = true;
            wallMomentum = Player.LinearVelocity.ProjectOnPlane(contactNormal);
            LimitWallMomentumToDownwardSlide();
        }
        else if (TryGetWallContactPoint(out wallContactPoint, out contactNormal))
        {
            foundWallContact = true;
        }

        if (!foundWallContact)
        {
            EndWallStick();
            return;
        }

        wallNormal = contactNormal;
        Player.GlobalPosition = wallContactPoint + wallNormal * GetWallDistance();
        WallSlideDustParticles.GlobalPosition = Player.GlobalPosition;
        if (PreserveInitialMomentum)
        {
            wallMomentum = wallMomentum.ProjectOnPlane(wallNormal);
            wallMomentum += Player.Gravity.ProjectOnPlane(wallNormal) * (float)delta;
            LimitWallMomentumToDownwardSlide();
            var slideDirection = Player.Gravity.ProjectOnPlane(wallNormal).Normalized();
            if (!slideDirection.IsZeroApprox())
            {
                var slideSpeed = wallMomentum.Dot(slideDirection);
                if (slideSpeed > SlideSpeed)
                    wallMomentum += slideDirection * (SlideSpeed - slideSpeed);
            }
            Player.LinearVelocity = wallMomentum;
        }
        else
        {
            var slideDirection = Player.Gravity.ProjectOnPlane(wallNormal);
            Player.LinearVelocity = slideDirection.IsZeroApprox()
                ? Vector3.Zero
                : slideDirection.Normalized() * SlideSpeed;
        }
        UpdateWallSlideDustScale();
        UpdateWallAnimation();
    }

    public void BeginWallStick(Vector3 normal)
    {
        BeginWallStick(normal, null);
    }

    private void BeginWallStick(Vector3 normal, Vector3? knownContactPoint)
    {
        if (IsWallSticking || Player.Grounded || normal.IsZeroApprox() || !CanBeginWallStick())
            return;

        wallNormal = normal.Normalized();
        wallMomentum = PreserveInitialMomentum
            ? Player.LinearVelocity.ProjectOnPlane(wallNormal)
            : Vector3.Zero;
        Vector3 wallContactPoint;
        Vector3 contactNormal;
        if (knownContactPoint.HasValue)
        {
            wallContactPoint = knownContactPoint.Value;
            contactNormal = normal;
        }
        else if (!TryGetWallContactPoint(out wallContactPoint, out contactNormal))
        {
            wallNormal = Vector3.Zero;
            wallMomentum = Vector3.Zero;
            return;
        }

        wallNormal = contactNormal;
        wallMomentum = wallMomentum.ProjectOnPlane(wallNormal);
        LimitWallMomentumToDownwardSlide();
        IsWallSticking = true;
        Player.Grounded = false;
        Player.noGroundTimer = 0;
        Player.DisableGroundRayTime = 0.1f;
        Player.DisableVelocityDamping();
        ActionRoll?.StopRoll();
        WallSlideDustParticles.Emitting = true;
        Player.GlobalPosition = wallContactPoint + wallNormal * GetWallDistance();
        WallSlideDustParticles.GlobalPosition = Player.GlobalPosition;
        Player.LinearVelocity = wallMomentum;
        UpdateWallAnimation();
        Player.PlayerSkinController.TravelAnimation(CurrentWallAnimation, true);
    }

    private bool CanBeginWallStick()
    {
        return CanProcess()
            && !(ActionHoming?.IsHoming ?? false)
            && !Player.PlayerInput.LockedInput
            && Player.PlayerInput.LeftInputTimedLock <= 0;
    }

    public void EndWallStick()
    {
        if (!IsWallSticking)
            return;

        IsWallSticking = false;
        CurrentWallAnimation = "WallStick";
        wallNormal = Vector3.Zero;
        wallMomentum = Vector3.Zero;
        WallSlideDustParticles.Emitting = false;
        Player.RestoreVelocityDamping();
        Player.PlayerSkinController.TravelAnimation("Air", true);
    }

    private void WallJump()
    {
        var upDirection = -Player.Gravity.Normalized();
        var preservedSlidingVelocity = Player.LinearVelocity.ProjectOnPlane(upDirection);
        var jumpDirection = (-Player.Gravity.Normalized() + wallNormal).Normalized();
        EndWallStick();
        Player.GroundNormal = -Player.Gravity.Normalized();
        Player.Grounded = false;
        Player.DisableGroundRayTime = 0.2f;
        Player.noGroundTimer = 0.1f;
        Player.LinearVelocity = jumpDirection * WallJumpVelocity + preservedSlidingVelocity;
        anyWallStickCooldown = WallJumpInputLockTime;
        Player.TimedLerpedRotation = 0.2f;
        Player.PlayerInput.LeftInput3D = Vector3.Zero;
        Player.PlayerInput.LeftRawInput = Vector2.Zero;
        Player.PlayerInput.PrevLeftRawInput = Vector2.Zero;
        Player.PlayerInput.LeftRawFlickInput = Vector2.Zero;
        Player.PlayerInput.LeftInputTimedLock = WallJumpInputLockTime;
        ActionJump?.PrepareForWallJump();
        ActionJump?.PlayJumpSound();
        ActionRoll?.StopRoll();
        Player.PlayerSkinController.TravelAnimation("Air", true);
    }

    private bool TryGetWallContactPoint(out Vector3 contactPoint, out Vector3 contactNormal)
    {
        var wallDistance = GetWallDistance();
        var rayStart = Player.GlobalPosition + wallNormal * 0.05f;
        var rayEnd = Player.GlobalPosition - wallNormal * (wallDistance + 2f);
        var rayQuery = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd, 1);
        rayQuery.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
        var rayResult = Player.GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);
        if (rayResult.Count == 0)
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }
        if (IsNoWalkCollider(rayResult))
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }

        contactPoint = rayResult["position"].AsVector3();
        contactNormal = rayResult["normal"].AsVector3().Normalized();
        if (contactNormal.Dot(Player.GlobalPosition - contactPoint) < 0)
            contactNormal = -contactNormal;
        if (!wallNormal.IsZeroApprox() && contactNormal.Dot(wallNormal) < 0.5f)
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }
        return true;
    }

    private bool TryGetFrontWallContactPoint(
        out Vector3 contactPoint,
        out Vector3 contactNormal
    )
    {
        var upDirection = -Player.Gravity.Normalized();
        var forwardDirection = Player.LinearVelocity.ProjectOnPlane(upDirection).Normalized();
        if (forwardDirection.IsZeroApprox() || Player.LinearVelocity.ProjectOnPlane(upDirection).Length() < WallRunSpeed)
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }

        var rayLength = Mathf.Max(GetWallDistance() + 1.5f, 1f);
        var rayQuery = PhysicsRayQueryParameters3D.Create(
            Player.GlobalPosition + forwardDirection * 0.05f,
            Player.GlobalPosition + forwardDirection * rayLength,
            1
        );
        rayQuery.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
        var rayResult = Player.GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);
        if (rayResult.Count == 0 || IsNoWalkCollider(rayResult))
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }

        contactPoint = rayResult["position"].AsVector3();
        contactNormal = rayResult["normal"].AsVector3().Normalized();
        if (contactNormal.Dot(Player.GlobalPosition - contactPoint) < 0)
            contactNormal = -contactNormal;
        if (
            Mathf.Abs(contactNormal.Dot(upDirection)) > 0.25f
            || contactNormal.Dot(forwardDirection) > -0.1f
        )
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }

        return true;
    }

    private bool TryGetAnyWallContactPoint(
        out Vector3 contactPoint,
        out Vector3 contactNormal
    )
    {
        var upDirection = -Player.Gravity.Normalized();
        var referenceDirection = Player.GlobalBasis.X.ProjectOnPlane(upDirection).Normalized();
        if (referenceDirection.IsZeroApprox())
            referenceDirection = Vector3.Right.ProjectOnPlane(upDirection).Normalized();
        if (referenceDirection.IsZeroApprox())
        {
            contactPoint = Vector3.Zero;
            contactNormal = Vector3.Zero;
            return false;
        }

        var tangentDirection = upDirection.Cross(referenceDirection).Normalized();
        var rayLength = Mathf.Max(GetWallDistance() + 0.5f, 0.5f);
        for (var index = 0; index < 16; index++)
        {
            var angle = Mathf.Tau * index / 16f;
            var rayDirection = (
                    referenceDirection * Mathf.Cos(angle)
                    + tangentDirection * Mathf.Sin(angle)
                ).Normalized();
            var rayStart = Player.GlobalPosition + rayDirection * 0.05f;
            var rayEnd = Player.GlobalPosition - rayDirection * rayLength;
            var rayQuery = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd, 1);
            rayQuery.Exclude = new Godot.Collections.Array<Rid> { Player.GetRid() };
            var rayResult = Player.GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);
            if (rayResult.Count == 0)
                continue;
            if (IsNoWalkCollider(rayResult))
                continue;

            var hitPoint = rayResult["position"].AsVector3();
            var hitNormal = rayResult["normal"].AsVector3().Normalized();
            if (hitNormal.Dot(Player.GlobalPosition - hitPoint) < 0)
                hitNormal = -hitNormal;
            if (Mathf.Abs(hitNormal.Dot(upDirection)) > 0.25f)
                continue;
            if (Player.LinearVelocity.Dot(hitNormal) > 0.1f)
                continue;

            contactPoint = hitPoint;
            contactNormal = hitNormal;
            return true;
        }

        contactPoint = Vector3.Zero;
        contactNormal = Vector3.Zero;
        return false;
    }

    private static bool IsNoWalkCollider(Godot.Collections.Dictionary rayResult)
    {
        return rayResult["collider"].AsGodotObject() is CollisionObject3D collider
            && collider.GetCollisionLayerValue(PlayerController.NO_WALK_GROUND_LAYER);
    }

    private void LimitWallMomentumToDownwardSlide()
    {
        var upDirection = -Player.Gravity.Normalized();
        var upwardSpeed = wallMomentum.Dot(upDirection);
        if (upwardSpeed > 0)
            wallMomentum -= upDirection * upwardSpeed;
    }

    private void UpdateWallSlideDustScale()
    {
        if (WallSlideDustParticles?.ProcessMaterial == null)
            return;

        var slideDirection = Player.Gravity.ProjectOnPlane(wallNormal).Normalized();
        var slidingSpeed = slideDirection.IsZeroApprox()
            ? 0f
            : Mathf.Abs(Player.LinearVelocity.Dot(slideDirection));
        var particleScale = Mathf.Clamp(slidingSpeed / 6f, 0.03f, 0.5f);
        WallSlideDustParticles.ProcessMaterial.Set("scale_min", particleScale);
        WallSlideDustParticles.ProcessMaterial.Set("scale_max", particleScale);
    }

    private void FaceAwayFromWall()
    {
        var up = -Player.Gravity.Normalized();
        var facingDirection = -wallNormal.ProjectOnPlane(up).Normalized();
        if (facingDirection.IsZeroApprox())
            return;

        var lookTransform = Player.GlobalTransform.LookingAt(
            Player.GlobalPosition + facingDirection,
            up
        );
        Player.GlobalTransform = new Transform3D(lookTransform.Basis, Player.GlobalPosition);
    }

    private void UpdateWallAnimation()
    {
        var upDirection = -Player.Gravity.Normalized();
        var tangentialVelocity = Player.LinearVelocity.ProjectOnPlane(upDirection);
        if (UseWallRunAnimations && tangentialVelocity.Length() >= WallRunSpeed)
        {
            var wallFacingDirection = -wallNormal.ProjectOnPlane(upDirection).Normalized();
            var wallRightDirection = wallFacingDirection.Cross(upDirection).Normalized();
            CurrentWallAnimation =
                tangentialVelocity.Dot(wallRightDirection) >= 0 ? "WallRunR" : "WallRunL";
            FaceTowardsVelocity(tangentialVelocity, upDirection);
            return;
        }

        CurrentWallAnimation = "WallStick";
        FaceAwayFromWall();
    }

    private float GetWallDistance()
    {
        var upDirection = -Player.Gravity.Normalized();
        var tangentialSpeed = Player.LinearVelocity.ProjectOnPlane(upDirection).Length();
        return Mathf.Max(
            tangentialSpeed >= WallRunSpeed ? WallRunDistance : WallSlideDistance,
            0f
        );
    }

    private void FaceTowardsVelocity(Vector3 velocity, Vector3 upDirection)
    {
        var facingDirection = velocity.ProjectOnPlane(upDirection).Normalized();
        if (facingDirection.IsZeroApprox())
            return;

        var lookTransform = Player.GlobalTransform.LookingAt(
            Player.GlobalPosition + facingDirection,
            upDirection
        );
        Player.GlobalTransform = new Transform3D(lookTransform.Basis, Player.GlobalPosition);
    }
}