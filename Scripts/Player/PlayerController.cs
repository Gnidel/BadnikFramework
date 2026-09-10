using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class PlayerController : RigidBody3D
{
    // TODO: Separate physics from this.

    // Global access stuff
    public static List<PlayerController> Instances = new List<PlayerController>();
    public int PlayerID;

    [Export]
    public string CharacterName = "You";

    [Export]
    public Color CharacterThemeColor = new Color(1, 1, 1, 1);

    // Components
    [Export]
    public PlayerInput PlayerInput;

    [Export]
    public PlayerInventory PlayerInventory;

    [Export]
    public PlayerDamage PlayerDamage;

    [Export]
    public NpcPartnerControl NpcPartnerControl;

    [Export]
    public PlayerSkinController PlayerSkinController;

    // Player physics variables
    [Export]
    public Curve Acceleration; // Key: VelocityXZ, Value: meters per second

    [Export]
    public float SpeedMultiplier = 1f;

    [Export]
    public bool DisableNoInputDecceleration = false;

    [Export]
    public float MinSpeed = 0;

    [Export]
    public float MaxSpeed = 200; // Caution: if it's too much without increasing physics ticks, you risk clipping through walls.

    [Export]
    public Curve NoInputDecceleration; // Key: Left thumbstick magnitude, Value: how fast decceleration happens

    [Export]
    public Vector3 Gravity = -Vector3.Up;

    [Export]
    public float GroundStickingPower = 3f;

    [Export]
    public float AntiInertiaFactor = 5f; // Basically turning speed. Lower it to make it hard to turn like in boost games.

    [Export]
    public bool OnlyFastRotation = false; // Turn without losing speed. This will make players unable to slow down on demand.

    [Export]
    public bool SkateMode = false; // Enables physics more designed around skateboarding. It works fine also on foot.

    [Export]
    public bool AllowGoingBackwards = false; // If it's disabled, it's not possible to go backwards, player will rotate. Intended for SkateMode

    [Export]
    public double DeltaMultiplier = 1;

    [Export]
    public float FlightUpDownSpeed = 2f;

    // Wallrun and slopes
    [Export]
    public float MinWallrunVelocityXZ = 30f;

    [Export]
    public float WallrunGravityDot = 0.3f;

    [Export]
    public float SlopeGravityFactor;

    [Export]
    public Curve SlopeRunFactor; // Key: Dot(Movement dir, Gravity), Value: Acceleration multiplier

    [Export]
    public float flatGroundDot = 0.999f;

    // Ground detection
    //[Export] public Vector3 RayOffset = Vector3.Zero;
    //[Export] public float GroundRayDistance = 0.05f;
    [Export]
    public RayCast3D RayCast;

    [Export]
    public double DisableGroundRayTime { get; set; }

    [Export]
    public double noGroundTimer { get; set; }

    [Export]
    public bool IsFlying;

    // Locks
    [Export]
    public double TimedLockRotation;
    public bool LockedRotation
    {
        get { return TimedLockRotation > 0; }
    }

    [Export]
    public double TimedLerpedRotation;

    [Export]
    public float TimedLerpedRotationSpeed = 10;
    public bool LerpedRotation
    {
        get { return TimedLerpedRotation > 0; }
    }

    [Export]
    public bool Enabled = true;

    // Utils and status
    public Vector3 GroundNormal = Vector3.Up;
    public bool Grounded;
    public bool GroundedLocked;
    public Vector3? GroundRayHitPoint;
    public uint ColliderLayer = 0;

    public float Leaning = 0; // How much player is trying to turn compared to how they are actually turning. +1 is max right, -1 is max left.

    // Evil stuff - murdering framerate at high speeds to avoid clipping
    int MinPhysicsTicksPerSecond = 60;
    int MaxPhysicsTicksPerSecond =
        60
        * 8 /* * 10*/
    ;
    int MinPhysicsStepsPerFrame = 8;
    int MaxPhysicsStepsPerFrame = 8 * 10;
    float BaselinePhysicsConfigSpeed = 20;
    float MaxPhysicsConfigSpeed = 400;

    // Special layers

    const int FLAT_GROUND_LAYER = 4;
    const int MOVING_PLATFORM_LAYER = 5;
    const int NO_WALK_GROUND_LAYER = 6;

    // Moving platform cache
    private Vector3 movingPlatformLastPosition = Vector3.Zero;
    private ulong movingPlatformInstanceID;
    public Vector3 lastMovingPlatformCorrection = Vector3.Zero;

    private Transform3D movingPlatformLastTransform;
    private Transform3D lastMovingPlatformCorrectionTransform;

    public Vector3 VelocityXZ
    {
        get { return Vector3Utils.ProjectOnPlane(this.LinearVelocity, GroundNormal); }
        set
        {
            this.LinearVelocity =
                Vector3Utils.Project(this.LinearVelocity, GroundNormal)
                + Vector3Utils.ProjectOnPlane(value, GroundNormal);
        }
    }
    public Vector3 VelocityY
    {
        get { return Vector3Utils.Project(this.LinearVelocity, GroundNormal); }
        set
        {
            this.LinearVelocity =
                Vector3Utils.Project(value, GroundNormal)
                + Vector3Utils.ProjectOnPlane(this.LinearVelocity, GroundNormal);
        }
    }

    public float VelocityYDirected
    {
        get
        {
            var velocityY = Vector3Utils.Project(this.LinearVelocity, GroundNormal);
            return velocityY.Dot(GroundNormal) > 0 ? velocityY.Length() : -VelocityY.Length();
        }
    }

    public override void _EnterTree()
    {
        PlayerID = Instances.Count;
        Instances.Add(this);
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Enabled)
            return;

        TimedLockRotation -= delta;
        if (TimedLockRotation < 0)
            TimedLockRotation = 0;

        TimedLerpedRotation -= delta;
        if (TimedLerpedRotation < 0)
            TimedLerpedRotation = 0;

        GroundDetection(delta);
        HandleInput(delta);

        bool lockedVelocity = LockedVelocityPhysics(delta * DeltaMultiplier);
        if (!lockedVelocity)
        {
            GeneralPhysics(delta * DeltaMultiplier);
        }
        //ConfigureEnginePhysics();
    }

    void GroundDetection(double delta)
    {
        var prevGrounded = Grounded;
        Vector3 hitPoint = this.GlobalPosition;
        Vector3 hitNormal = this.Basis.Y;
        DisableGroundRayTime -= delta;

        if (DisableGroundRayTime <= 0)
        {
            Grounded = RayCast.IsColliding();
            hitPoint = RayCast.GetCollisionPoint();
            hitNormal = RayCast.GetCollisionNormal();
        }

        if (prevGrounded && !Grounded)
        {
            //var exludedRids = new Godot.Collections.Array<Rid>(new Rid[] { this.GetRid() });
            //var backupRayResult = GetWorld3D().DirectSpaceState.IntersectRay(
            //PhysicsRayQueryParameters3D.Create(RayCast.GlobalPosition,
            //	RayCast.GlobalPosition + RayCast.GlobalBasis.GetRotationQuaternion() * (RayCast.TargetPosition * 5),
            //	RayCast.CollisionMask, exludedRids));
            //Grounded = backupRayResult.Count > 0;
            //if (Grounded)
            //{
            //	hitPoint = backupRayResult["position"].AsVector3();
            //	hitNormal = backupRayResult["normal"].AsVector3();
            //	GD.Print("Recovered");
            //}
            //else
            //{
            //	GD.Print("Reason for detachment: Raycast fail");
            //}
            if (GroundNormal.Normalized().Dot(-Gravity.Normalized()) < flatGroundDot) // Prevent player from sticking to the edge right after being launched from quarterpipe
            {
                DisableGroundRayTime = Mathf.Max(0.1f, DisableGroundRayTime);
            }
        }
        if (hitNormal.Dot(GroundNormal.Normalized()) < 0)
        {
            Grounded = false;
        }
        else if (Grounded && hitNormal.Dot(GroundNormal.Normalized()) < WallrunGravityDot)
        {
            Grounded = false;
        }

        //if (Grounded && RayCast.GetCollisionMaskValue(FLAT_GROUND_LAYER))   // Special case: floor tagged as flat, use -gravity instead of hit normal.
        bool isMovingPlatform = false;
        if (Grounded && DisableGroundRayTime <= 0)
        {
            CollisionObject3D c1 = RayCast.GetCollider() as CollisionObject3D;
            CsgShape3D c2 = RayCast.GetCollider() as CsgShape3D;

            bool isNoWalk =
                (c1 != null && c1.GetCollisionLayerValue(NO_WALK_GROUND_LAYER))
                || (c2 != null && c2.GetCollisionLayerValue(NO_WALK_GROUND_LAYER));

            bool isFlat =
                (c1 != null && c1.GetCollisionLayerValue(FLAT_GROUND_LAYER))
                || (c2 != null && c2.GetCollisionLayerValue(FLAT_GROUND_LAYER));

            isMovingPlatform =
                (c1 != null && c1.GetCollisionLayerValue(MOVING_PLATFORM_LAYER))
                || (c2 != null && c2.GetCollisionLayerValue(MOVING_PLATFORM_LAYER));
            if (isNoWalk)
            {
                Grounded = false;
            }
            else if (isFlat)
            {
                hitNormal = -Gravity.Normalized();
                if (hitNormal.Dot(-Gravity.Normalized()) < flatGroundDot)
                {
                    Grounded = false;
                }
            }

            ColliderLayer = 0;
            if (c1 != null)
            {
                ColliderLayer = c1.CollisionLayer;
            }
            if (c2 != null)
            {
                ColliderLayer = c2.CollisionLayer;
            }
        }

        //// Moving platforms system
        lastMovingPlatformCorrection = Vector3.Zero;
        if (Grounded && isMovingPlatform)
        {
            var collider = RayCast.GetCollider();
            if (collider != null)
            {
                var transformVariant = collider.Get("global_transform");
                if (transformVariant.Obj != null)
                {
                    var movingPlatformTransform = transformVariant.AsTransform3D();
                    ulong colliderId = collider.GetInstanceId();

                    if (movingPlatformInstanceID == colliderId)
                    {
                        // Full delta transform: how the platform moved AND rotated
                        // since last frame, around whatever axis it rotated on.
                        Transform3D deltaTransform =
                            movingPlatformTransform * movingPlatformLastTransform.AffineInverse();

                        Vector3 oldGlobalPosition = GlobalPosition;
                        Vector3 newGlobalPosition = deltaTransform * oldGlobalPosition;

                        lastMovingPlatformCorrection = newGlobalPosition - oldGlobalPosition;

                        // Apply the platform's full rotation delta to the character's
                        // orientation, not just a Y-axis slice. This is what makes it
                        // correct under arbitrary gravity/up directions.
                        Basis deltaBasis = deltaTransform.Basis.Orthonormalized();
                        Basis newBasis = deltaBasis * GlobalTransform.Basis;

                        GlobalTransform = new Transform3D(
                            newBasis,
                            oldGlobalPosition + lastMovingPlatformCorrection
                        );
                    }

                    movingPlatformLastTransform = movingPlatformTransform;
                    movingPlatformInstanceID = colliderId;
                }
            }
        }
        else
        {
            movingPlatformInstanceID = 0;
        }

        if (noGroundTimer > 0)
        {
            Grounded = false;
            noGroundTimer -= delta;
        }

        if (Grounded)
        {
            if (
                VelocityXZ.Length() < (MinWallrunVelocityXZ * SpeedMultiplier)
                && hitNormal.Normalized().Dot(-Gravity.Normalized()) < WallrunGravityDot
            )
            {
                Grounded = false;
                DisableGroundRayTime = Mathf.Max(DisableGroundRayTime, 0.2f);
                return; // Wait for a frame. This prevents clipping through ceiling by avoiding sudden 180 degree rotation, from one side of wall to another.
            }
            GroundRayHitPoint = hitPoint;
        }
        else
        {
            GroundRayHitPoint = null;
        }

        if (GroundRayHitPoint != null)
        {
            var distanceFromGround = Vector3Utils.LengthAlongsideVector(
                this.GlobalPosition - GroundRayHitPoint.Value,
                GroundNormal
            );

            if (
                (distanceFromGround < 0 || distanceFromGround > 0.1f)
                && (this.GetContactCount() <= 1)
            )
            {
                var touchesWall = false;
                foreach (var colBody in this.GetCollidingBodies())
                {
                    var collObject = colBody.GetNodeOrNull<CollisionObject3D>(".");

                    if (collObject != null && RayCast.GetColliderRid() != collObject.GetRid())
                    {
                        touchesWall = true;
                    }
                }

                //if (!touchesWall)
                {
                    //this.GlobalPosition = this.GlobalPosition.ProjectOnPlane(GroundNormal) + GroundRayHitPoint.Value.Project(GroundNormal);
                    this.MoveAndCollide(
                        this.GlobalPosition.ProjectOnPlane(GroundNormal)
                            + GroundRayHitPoint.Value.Project(GroundNormal)
                            - this.GlobalPosition
                    );
                }
            }
        }

        if (
            Grounded /*&& hit.collider.tag == "Untagged"*/
        )
        {
            //if (!prevGrounded)
            //{
            //	this.GlobalPosition = GroundRayHitPoint.Value;
            //}
            GroundNormal = hitNormal.Normalized();
            //if (GroundNormal.Dot(-Gravity.Normalized()) > flatGroundDot)
            //{
            //	GroundNormal = -Gravity;
            //}

            if (
                !prevGrounded
                || ( /*this.GlobalPosition.DistanceTo(hitPoint) > 0.2f &&*/
                    this.LinearVelocity.Normalized().Dot(GroundNormal.Normalized()) < -0.5f
                )
            )
            {
                this.GlobalPosition =
                    Vector3Utils.ProjectOnPlane(this.GlobalPosition, this.Basis.Y)
                    + Vector3Utils.Project(hitPoint, this.Basis.Y); //(this.GlobalPosition + hitPoint) / 2;
            }
        }
        else
        {
            if (prevGrounded)
            {
                this.LinearVelocity = this.LinearVelocity.ProjectOnPlane(GroundNormal); // Remove impact of sticking to ground
                //if (GroundNormal.Normalized().Dot(-Gravity.Normalized()) > 0.9999f)
                {
                    this.MoveAndCollide(GroundNormal.Normalized() * 0.5f); // Makes half-pipes feel better
                }
            }
            else // Give it a frame of grace
            {
                if (this.GetCollidingBodies().Count == 0)
                {
                    if (SkateMode)
                    {
                        if (VelocityYDirected < 0)
                        {
                            var rayQueryDown = PhysicsRayQueryParameters3D.Create(
                                this.GlobalPosition,
                                this.GlobalPosition + this.Gravity.Normalized() * 50f,
                                RayCast.CollisionMask
                            );

                            var rayDownResult = GetWorld3D()
                                .DirectSpaceState.IntersectRay(rayQueryDown);
                            if (rayDownResult.Count > 0)
                            {
                                var downHitNormal = rayDownResult["normal"].AsVector3();
                                if (GroundNormal.Normalized().Dot(downHitNormal.Normalized()) < 0) // Try to avoid being upside down. Snap to half-way point if necessary.
                                {
                                    GroundNormal = GroundNormal.Slerp(downHitNormal, 0.5f);
                                }
                                GroundNormal = GroundNormal
                                    .Slerp(downHitNormal, (float)delta * 10f)
                                    .Normalized();
                            }
                        }
                    }
                    else
                    {
                        GroundNormal = -Gravity.Normalized();
                    }
                }
                else
                {
                    GroundNormal = GroundNormal.Lerp(-Gravity.Normalized(), (float)delta * 5f);
                }
            }
        }

        var previousRotation = this.Transform;

        if (
            Grounded
            && GroundNormal.Dot(-Gravity.Normalized()) > flatGroundDot
            && VelocityXZ.Length() < 5
            && PlayerInput.LeftInput3D.Length() < 0.5f
        ) // TODO: Configure dead zone
        {
            this.LinearVelocity = Vector3Utils.Project(this.LinearVelocity, Gravity);
        }

        this.LinearVelocity = this.LinearVelocity.ClampLength(MinSpeed, MaxSpeed);

        var up = GroundNormal;
        var nextLook =
            previousRotation.Origin
            + (
                VelocityXZ.Length() > 0.1f ? VelocityXZ.Normalized() : -previousRotation.Basis.Z
            ).ProjectOnPlane(GroundNormal);

        if (Mathf.Abs((nextLook - previousRotation.Origin).Dot(GroundNormal)) >= 0.999f)
        {
            //nextLook = this.LinearVelocity.Normalized();
            up = this.Basis.Y; // Stuff got wonky when looking directly up/down. Plan B, Don't rotate.
            nextLook = -previousRotation.Basis.Z;
        }
        if ((nextLook - previousRotation.Origin).IsZeroApprox())
            return;

        if (up.IsZeroApprox())
        {
            return;
            //up = nextLook * Vector3.Up;
        }

        Transform3D newRotation = previousRotation.LookingAt(nextLook, up);
        if (AllowGoingBackwards) // On skate mode, allow going backwards
        {
            if ((nextLook - previousRotation.Origin).Normalized().Dot(-this.Basis.Z) < 0)
            {
                if (Grounded)
                {
                    newRotation = newRotation.RotatedLocal(Vector3.Up, Mathf.DegToRad(180f));
                }
                else
                {
                    TimedLerpedRotation = Mathf.Max(TimedLerpedRotation, 0.3f);
                }
            }
        }

        {
            Quaternion lerpedQuaternion;
            if (TimedLerpedRotation > 0)
            {
                lerpedQuaternion = previousRotation
                    .Basis.GetRotationQuaternion()
                    .Normalized()
                    .Slerp(
                        newRotation.Basis.GetRotationQuaternion().Normalized(),
                        (float)delta * TimedLerpedRotationSpeed
                    );
            }
            else
            {
                lerpedQuaternion = newRotation.Basis.GetRotationQuaternion().Normalized();
            }

            newRotation.Basis = new Basis(lerpedQuaternion);
        }

        if (LockedRotation)
        {
            newRotation = previousRotation;
        }

        this.Transform = newRotation;
        this.MoveAndCollide(previousRotation.Basis.Y - newRotation.Basis.Y);
    }

    void HandleInput(double delta)
    {
        Leaning = 0;
        if (this.PlayerInput.LockedInput)
            return;

        var leftThumbstickInput = PlayerInput.TwodimensionalMode
            ? Vector3Utils.ProjectOnPlane(PlayerInput.LeftInput3D, GroundNormal).Normalized()
                * PlayerInput.LeftInput3D.Length()
            : PlayerInput.LeftInput3D;

        bool ignoreAirborneSkateDirection =
            SkateMode && !Grounded && GroundNormal.Dot(-Gravity.Normalized()) < 0.9f;
        if (ignoreAirborneSkateDirection)
        {
            leftThumbstickInput = Vector3.Zero;
        }

        var slopeFactor = SlopeRunFactor.Sample(
            leftThumbstickInput.Normalized().Dot(Gravity.Normalized())
        );
        this.LinearVelocity +=
            leftThumbstickInput
            * (float)(
                Acceleration.Sample(
                    VelocityXZ.Length()
                        / 100f /*/ slopeFactor*/
                        / SpeedMultiplier
                )
                * SpeedMultiplier
                * delta
            );
        if (IsFlying)
        {
            float inputY = 0;
            if (PlayerInput.TwodimensionalMode)
            {
                inputY = PlayerInput.LeftRawInput.Y;
            }
            else
            {
                if (PlayerInput.IsActionPressed("actionroll"))
                {
                    inputY += 1f;
                }
                if (PlayerInput.IsActionPressed("actionjump"))
                {
                    inputY -= 1f;
                }
            }
            this.LinearVelocity +=
                -GroundNormal
                * inputY
                * (float)(
                    Acceleration.Sample(
                        VelocityXZ.Length()
                            / 100f /*/ slopeFactor*/
                            / SpeedMultiplier
                    )
                    * SpeedMultiplier
                    * delta
                )
                * FlightUpDownSpeed;
        }
        if (!ignoreAirborneSkateDirection)
        {
            RotateVelocityToInput(delta);
        }
    }

    void RotateVelocityToInput(double delta)
    {
        var groundNormal = Grounded ? GroundNormal : -Gravity.Normalized();
        var axis = groundNormal.Normalized();

        var leftThumbstickInput =
            Vector3Utils.ProjectOnPlane(PlayerInput.LeftInput3D, groundNormal).Normalized()
            * PlayerInput.LeftInput3D.Length();
        if (leftThumbstickInput.Length() < 0.5f)
            return;

        var XZ = Vector3Utils.ProjectOnPlane(this.LinearVelocity, groundNormal);
        if (XZ.Length() < 0.5f)
        {
            Leaning = 0;
            return;
        }
        var Y = Vector3Utils.Project(this.LinearVelocity, groundNormal);

        var initialXZ = XZ;

        var from = XZ.Normalized();
        var to = leftThumbstickInput.Normalized();

        // Signed angle from `from` to `to` about `axis`, via Atan2. Unlike Quaternion(from, to)
        // - which derives its rotation axis from Cross(from, to) and breaks down when that
        //   cross product is (near) zero, i.e. exactly at the flat ceiling -
        // this stays continuous and well-defined for every angle, including 180 degrees.
        float signedAngle = Mathf.Atan2(from.Cross(to).Dot(axis), from.Dot(to));

        var targetRotation = new Quaternion(axis, signedAngle).Normalized();

        // fastXZ: only rotate by a fraction of the full angle this frame.
        // (Equivalent to Slerp(Identity, targetRotation, t) since it's a fixed axis.)
        var fastXZ =
            new Quaternion(axis, signedAngle * (float)delta * AntiInertiaFactor).Normalized() * XZ;

        // preciseXZ: apply the full rotation, then linearly blend the vector toward it (gentler ramp).
        var preciseXZ = XZ.Lerp(targetRotation * XZ, (float)delta * AntiInertiaFactor);

        if ((this.OnlyFastRotation) || (this.Grounded && to.Dot(from) > -0.5f))
        {
            XZ = fastXZ;
        }
        else
        {
            XZ = preciseXZ;
        }

        this.Leaning = Mathf.Clamp(
            Vector3Utils.LengthAlongsideVector((XZ - initialXZ), this.GlobalBasis.X),
            -1,
            1
        );

        this.LinearVelocity = XZ + Y;
    }

    void GeneralPhysics(double delta)
    {
        if (Grounded)
        {
            if (
                !PlayerInput.LockedInput
                && !DisableNoInputDecceleration
                && GroundNormal.Normalized().Dot(-Gravity.Normalized()) > WallrunGravityDot
            )
            {
                //var breakFactor = Mathf.Clamp(PlayerInput.LeftInput3D.Normalized().Dot(VelocityXZ.Normalized()), -1, 1);
                this.VelocityXZ = this.VelocityXZ.Lerp(
                    Vector3.Zero,
                    NoInputDecceleration.Sample(
                        PlayerInput.LeftInput3D.Length()
                    /** breakFactor*/) * (float)delta
                );
            }
            if (this.LinearVelocity.Length() < 0.1f)
                this.LinearVelocity = Vector3.Zero;

            this.LinearVelocity +=
                Vector3Utils.ProjectOnPlane(Gravity, GroundNormal)
                * (float)delta
                * SlopeGravityFactor;
            if (this.LinearVelocity.Dot(GroundNormal) <= 0)
            {
                this.LinearVelocity = Vector3Utils.ProjectOnPlane(
                    this.LinearVelocity,
                    GroundNormal
                );
            }

            //if (GroundNormal.Normalized().Dot(-Gravity.Normalized()) < flatGroundDot)
            {
                this.LinearVelocity -= GroundNormal.Normalized() * GroundStickingPower;
            }
        }
        //else if (LegsTouchGround)
        //{
        //    this.LinearVelocity += Gravity * (float)delta * Mathf.Clamp((MinWallrunVelocityXZ - this.LinearVelocity.magnitude), 0, MinWallrunVelocityXZ) / MinWallrunVelocityXZ;
        //}
        else if (!IsFlying)
        {
            this.LinearVelocity += Gravity * (float)delta;
        }
    }

    Vector3 VelocityLockInitialPosition = Vector3.Zero;
    float VelocityLockDistance = 0;
    float VelocityLockTime = 0;
    Vector3 VelocityLockVelocity = Vector3.Zero;

    public void LockVelocity(float distance, float time)
    {
        VelocityLockInitialPosition = this.GlobalPosition;
        VelocityLockVelocity = this.LinearVelocity;
        VelocityLockDistance = distance;
        VelocityLockTime = time;
    }

    bool LockedVelocityPhysics(double delta) // Return: is locked
    {
        if (VelocityLockTime <= 0 && VelocityLockDistance <= 0)
            return false;
        bool isLocked = false;
        if (VelocityLockDistance > 0)
        {
            var distance = this.GlobalPosition.DistanceTo(VelocityLockInitialPosition);
            if (distance > VelocityLockDistance)
            {
                VelocityLockDistance = 0;
            }
            else
            {
                isLocked = true;
            }
        }
        if (VelocityLockTime > 0)
        {
            VelocityLockTime -= (float)delta;
            if (VelocityLockTime < 0)
            {
                VelocityLockTime = 0;
            }
            else
            {
                isLocked = true;
            }
        }

        if (isLocked)
        {
            this.LinearVelocity = VelocityLockVelocity;
        }
        else
        {
            VelocityLockVelocity = Vector3.Zero;
        }

        return isLocked;
    }

    public bool LockedVelocity
    {
        get { return VelocityLockTime > 0 || VelocityLockDistance > 0; }
    }

    void ConfigureEnginePhysics()
    {
        /*
         *	This section contains pure evil - we mess up with physics speed. At high speeds we torture the hardware in order
         *	to avoid clipping through walls. At lower we go to more baseline calculations.
         *
         *	TODO: Make it somehow compatible with multiplayer.
         */
        if (this.PlayerID != 0)
            return;
        if (BaselinePhysicsConfigSpeed == MaxPhysicsConfigSpeed)
            return;

        var speed = this.LinearVelocity.Length();
        var physicsSpeedRatio =
            (speed - BaselinePhysicsConfigSpeed)
            / (MaxPhysicsConfigSpeed - BaselinePhysicsConfigSpeed);
        physicsSpeedRatio = Mathf.Clamp(physicsSpeedRatio, 0, 1);

        Engine.PhysicsTicksPerSecond = (int)(
            Mathf.Lerp(
                (float)MinPhysicsTicksPerSecond,
                (float)MaxPhysicsTicksPerSecond,
                physicsSpeedRatio
            )
        );
        Engine.MaxPhysicsStepsPerFrame = (int)(
            Mathf.Lerp(
                (float)MinPhysicsStepsPerFrame,
                (float)MaxPhysicsStepsPerFrame,
                physicsSpeedRatio
            )
        );
    }
}
