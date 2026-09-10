using Godot;
using System;

public partial class BigArmControl : Node3D
{
	[Export] public Area3D GrabArea;
	[Export] public AnimationTree AnimationTree;
	[Export] public Node3D PlayerCatchPos;
	[Export] public Node3D DeathEggRoot;
	[Export] public float RepositionSpeed = 1f;
	[Export] public float PredictionTime = 1.5f;
	[Export] public float AheadDistance = 35f;
	[Export] public float TryCatchLookSpeed = 5f;
	[Export] public float TryCatchDistance = 12f;
	[Export] public float TryCatchDuration = 3f;
	[Export] public float TryCatchNearbyDistance = 150f;
	[Export] public float RepositionVerticalRange = 20f;
	[Export] public float RepositionVerticalSpeed = 30f;
	[Export] public float CatchDuration = 1f;
	[Export] public float PrepareThrowDuration = 1f;
	[Export] public float ThrowDuration = 1f;
	[Export] public float ByeByeDuration = 5f;
	[Export] public float RestDuration = 3f;
	[Export] public float ThrowSpeed = 500f;
	[Export] public double ControlDisableTime = 3d;

	private State CurrentState = State.TRY_CATCH;
	private double StateTime;
	private ActionAutoGimmick CaughtPlayerAction;
	private PlayerController CaughtPlayer;
	private float CatchStartRotationY;
	private float CatchTargetRotationY;
	private Vector3 RepositionTarget;
	private bool HasRepositionTarget;

	private enum State
	{
		REST,
		TRY_CATCH,
		CATCH,
		PrepareThrow,
		THROW,
		BYEBYE
	}

	public override void _PhysicsProcess(double delta)
	{
		StateTime += delta;
		GrabArea.ProcessMode = (CurrentState == State.TRY_CATCH) ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
		AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
				AnimationTree.Get("parameters/playback");

		switch (CurrentState)
		{
			case State.REST:
				stateMachine.Travel("Rest");
				if (!HasRepositionTarget)
					SelectRepositionTarget();

				if (HasRepositionTarget)
				{
					LookAtRepositionTarget(delta);
					MoveToRepositionTarget(delta);
					if (IsAtRepositionTarget())
					{
						if (
							TryGetTargetPlayer(out PlayerController repositionPlayer)
							&& IsPlayerNearby(repositionPlayer)
							&& IsPlayerApproaching(repositionPlayer)
						)
						{
							SetState(State.TRY_CATCH);
						}
						else
						{
							HasRepositionTarget = false;
							StateTime = 0;
						}
					}
				}
				break;
			case State.TRY_CATCH:
				stateMachine.Travel("TryCatch");
				if (TryGetTargetPlayer(out PlayerController targetPlayer))
				{
					LookAtPlayer(targetPlayer, delta);
					if (
						StateTime >= TryCatchDuration
						&& HasPlayerPassed(targetPlayer)
					)
						SetState(State.REST);
				}
				break;
			case State.CATCH:
				stateMachine.Travel("Catch");
				UpdateCatchRotation();
				if (StateTime >= CatchDuration)
					SetState(State.PrepareThrow);
				break;
			case State.PrepareThrow:
				stateMachine.Travel("PrepareThrow");
				if (StateTime >= PrepareThrowDuration)
					SetState(State.THROW);
				break;
			case State.THROW:
				stateMachine.Travel("Throw");
				if (StateTime >= ThrowDuration)
					SetState(State.BYEBYE);
				break;
			case State.BYEBYE:
				stateMachine.Travel("UglyGesture");
				if (StateTime >= ByeByeDuration)
					SetState(State.REST);
				break;


		}
	}

	public void OnGrabAreaEnter(Node3D other)
	{
		if (CurrentState != State.TRY_CATCH)
			return; // Should never happen outside of 2 players entering at the same time

		var player = other.GetNodeOrNull<PlayerController>(".");
		if (player == null)
			return;

		var actionAutoGimmick = player.GetNodeOrNull<ActionAutoGimmick>(
                "./PlayerControl/Actions/ActionAutoGimmick"
			);

		if (actionAutoGimmick == null)
			return;

		actionAutoGimmick.StartAutoGimmick("Rolling", PlayerCatchPos, false);
		CaughtPlayer = player;
		CaughtPlayerAction = actionAutoGimmick;
		SetState(State.CATCH);
	}

	private void SetState(State nextState)
	{
		CurrentState = nextState;
		StateTime = 0;

		if (nextState == State.CATCH)
		{
			Vector3 outward = GlobalPosition;
			outward.Y = 0;
			if (outward.IsZeroApprox())
				outward = -GlobalBasis.Z;

			outward = outward.Normalized();
			CatchStartRotationY = GlobalRotation.Y;
			CatchTargetRotationY = Mathf.Atan2(-outward.X, -outward.Z);
		}
		else if (nextState == State.REST)
		{
			HasRepositionTarget = false;
		}
		else if (nextState == State.THROW)
		{
			if (CaughtPlayerAction != null && CaughtPlayer != null)
			{
				PlayerController player = CaughtPlayer;
				Vector3 throwDirection = CaughtPlayer.GlobalPosition - GetDeathEggCenter();
				throwDirection = throwDirection.Normalized();
				if (throwDirection.IsZeroApprox())
				{
					throwDirection = GlobalPosition - GetDeathEggCenter();
					throwDirection = throwDirection.Normalized();
				}

				CaughtPlayerAction.EndAutoGimmick("Damage");
				player.PlayerInput.LeftInput3D = Vector3.Zero;
				player.PlayerInput.LeftRawInput = Vector2.Zero;
				player.PlayerInput.LockedInput = true;
				player.PlayerInput.LeftInputTimedLock = ControlDisableTime;
				player.LinearVelocity = throwDirection * ThrowSpeed;
				ReleasePlayerControlsAfterThrow(player);
			}

			CaughtPlayerAction = null;
			CaughtPlayer = null;
		}
	}

	private void ReleasePlayerControlsAfterThrow(PlayerController player)
	{
		if (ControlDisableTime <= 0)
		{
			player.PlayerInput.LockedInput = false;
			return;
		}

		GetTree().CreateTimer(ControlDisableTime).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(player))
				player.PlayerInput.LockedInput = false;
		};
	}

	private bool TryGetTargetPlayer(out PlayerController targetPlayer)
	{
		targetPlayer = null;
		foreach (var player in PlayerController.Instances)
		{
			if (player.NpcPartnerControl != null && player.NpcPartnerControl.IsNpc)
				continue;

			targetPlayer = player;
			return true;
		}

		return false;
	}

	private Vector3 GetDeathEggCenter()
	{
		return DeathEggRoot == null ? Vector3.Zero : DeathEggRoot.GlobalPosition;
	}

	private float GetEquatorRadius()
	{
		Vector3 offset = GlobalPosition - GetDeathEggCenter();
		offset.Y = 0;
		return Mathf.Max(offset.Length(), 0.01f);
	}

	private float GetPlayerAngle(PlayerController player)
	{
		Vector3 offset = player.GlobalPosition - GetDeathEggCenter();
		offset.Y = 0;
		return Mathf.Atan2(offset.X, offset.Z);
	}

	private float GetPlayerAngularDirection(PlayerController player)
	{
		Vector3 offset = player.GlobalPosition - GetDeathEggCenter();
		offset.Y = 0;
		if (offset.IsZeroApprox())
			return 1f;

		Vector3 tangent = new Vector3(offset.Z, 0, -offset.X).Normalized();
		float tangentVelocity = player.LinearVelocity.Dot(tangent);
		if (Mathf.Abs(tangentVelocity) > 0.1f)
			return Mathf.Sign(tangentVelocity);

		return 1f;
	}

	private float GetAheadAngle(PlayerController player)
	{
		Vector3 predictedPosition = player.GlobalPosition + player.LinearVelocity * PredictionTime;
		Vector3 offset = predictedPosition - GetDeathEggCenter();
		offset.Y = 0;
		if (offset.IsZeroApprox())
			return GetPlayerAngle(player);

		float predictedAngle = Mathf.Atan2(offset.X, offset.Z);
		return predictedAngle + GetPlayerAngularDirection(player) * AheadDistance / GetEquatorRadius();
	}

	private void SelectRepositionTarget()
	{
		if (!TryGetTargetPlayer(out PlayerController player))
			return;

		Vector3 center = GetDeathEggCenter();
		float targetAngle = GetAheadAngle(player);
		float radius = GetEquatorRadius();
		float targetY = Mathf.Clamp(
			(player.GlobalPosition + player.LinearVelocity * PredictionTime).Y,
			center.Y - RepositionVerticalRange,
			center.Y + RepositionVerticalRange
		);
		RepositionTarget = new Vector3(
			center.X + Mathf.Sin(targetAngle) * radius,
			targetY,
			center.Z + Mathf.Cos(targetAngle) * radius
		);
		HasRepositionTarget = true;
	}

	private void MoveToRepositionTarget(double delta)
	{
		Vector3 center = GetDeathEggCenter();
		float currentAngle = Mathf.Atan2(
			GlobalPosition.X - center.X,
			GlobalPosition.Z - center.Z
		);
		float targetAngle = Mathf.Atan2(
			RepositionTarget.X - center.X,
			RepositionTarget.Z - center.Z
		);
		float angleStep = Mathf.Clamp(
			Mathf.AngleDifference(currentAngle, targetAngle),
			-RepositionSpeed * (float)delta,
			RepositionSpeed * (float)delta
		);
		SetEquatorAngle(currentAngle + angleStep);

		Vector3 position = GlobalPosition;
		position.Y = Mathf.MoveToward(
			position.Y,
			RepositionTarget.Y,
			RepositionVerticalSpeed * (float)delta
		);
		GlobalPosition = position;
	}

	private void LookAtRepositionTarget(double delta)
	{
		Vector3 direction = RepositionTarget - GlobalPosition;
		if (direction.IsZeroApprox())
			return;

		float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = GlobalRotation;
		rotation.Y = Mathf.LerpAngle(
			rotation.Y,
			targetAngle,
			Mathf.Clamp(TryCatchLookSpeed * (float)delta, 0f, 1f)
		);
		GlobalRotation = rotation;
	}

	private bool IsAtRepositionTarget()
	{
		Vector3 center = GetDeathEggCenter();
		float currentAngle = Mathf.Atan2(
			GlobalPosition.X - center.X,
			GlobalPosition.Z - center.Z
		);
		float targetAngle = Mathf.Atan2(
			RepositionTarget.X - center.X,
			RepositionTarget.Z - center.Z
		);
		return Mathf.Abs(Mathf.AngleDifference(currentAngle, targetAngle)) < 0.01f
			&& Mathf.Abs(GlobalPosition.Y - RepositionTarget.Y) < 0.5f;
	}

	private void SetEquatorAngle(float angle)
	{
		Vector3 center = GetDeathEggCenter();
		float radius = GetEquatorRadius();
		Vector3 position = GlobalPosition;
		position.X = center.X + Mathf.Sin(angle) * radius;
		position.Z = center.Z + Mathf.Cos(angle) * radius;
		GlobalPosition = position;
	}

	private void LookAtPlayer(PlayerController player, double delta)
	{
		Vector3 direction = player.GlobalPosition - GlobalPosition;
		direction.Y = 0;
		if (direction.IsZeroApprox())
			return;

		float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = GlobalRotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, TryCatchLookSpeed * (float)delta);
		GlobalRotation = rotation;
	}

	private bool HasPlayerPassed(PlayerController player)
	{
		return GetPlayerAheadAngle(player) > TryCatchDistance / GetEquatorRadius();
	}

	private bool IsPlayerNearby(PlayerController player)
	{
		Vector3 offset = player.GlobalPosition - GlobalPosition;
		offset.Y = 0;
		return offset.Length() <= TryCatchNearbyDistance;
	}

	private bool IsPlayerApproaching(PlayerController player)
	{
		return GetPlayerAheadAngle(player) < 0;
	}

	private float GetPlayerAheadAngle(PlayerController player)
	{
		float direction = GetPlayerAngularDirection(player);
		float armAngle = Mathf.Atan2(
			GlobalPosition.X - GetDeathEggCenter().X,
			GlobalPosition.Z - GetDeathEggCenter().Z
		);
		return Mathf.AngleDifference(armAngle, GetPlayerAngle(player)) * direction;
	}

	private void UpdateCatchRotation()
	{
		float rotationProgress = CatchDuration <= 0
			? 1f
			: Mathf.Clamp((float)(StateTime / CatchDuration), 0f, 1f);
		Vector3 rotation = GlobalRotation;
		rotation.Y = Mathf.LerpAngle(CatchStartRotationY, CatchTargetRotationY, rotationProgress);
		GlobalRotation = rotation;
	}
}
