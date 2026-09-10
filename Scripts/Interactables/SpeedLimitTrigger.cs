using Godot;
using System;
using System.Collections.Generic;

public partial class SpeedLimitTrigger : Area3D
{
	[Export]
	public Vector3 Axis = Vector3.Forward;

	[Export]
	public Path3D Path;

	[Export]
	public float MinSpeed = 0f;

	[Export]
	public float MaxSpeed = 200f;

	private readonly HashSet<PlayerController> players = new();

	public override void _Ready()
	{
		if (GetSignalConnectionList("body_entered").Count == 0)
		{
			BodyEntered += OnBodyEnter;
		}

		if (GetSignalConnectionList("body_exited").Count == 0)
		{
			BodyExited += OnBodyExit;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		players.RemoveWhere(player => !GodotObject.IsInstanceValid(player) || !player.IsInsideTree());

		foreach (var player in players)
		{
			var axis = GetAxis(player);
			if (axis.IsZeroApprox())
			{
				continue;
			}

			var velocity = player.LinearVelocity;
			var speedAlongAxis = velocity.Dot(axis);
			var speedSign = Mathf.Sign(speedAlongAxis);
			var clampedSpeed = Mathf.Clamp(Mathf.Abs(speedAlongAxis), MinSpeed, MaxSpeed);
			player.LinearVelocity = velocity + axis * (clampedSpeed * speedSign - speedAlongAxis);
		}
	}

	public void OnBodyEnter(Node3D other)
	{
		var player = other.GetNodeOrNull<PlayerController>(".");
		if (player != null)
		{
			players.Add(player);
		}
	}

	public void OnBodyExit(Node3D other)
	{
		var player = other.GetNodeOrNull<PlayerController>(".");
		if (player != null)
		{
			players.Remove(player);
		}
	}

	private Vector3 GetAxis(PlayerController player)
	{
		if (Path == null || Path.Curve == null)
		{
			return Axis.Normalized();
		}

		var localPosition = Path.GlobalTransform.AffineInverse() * player.GlobalPosition;
		var offset = Path.Curve.GetClosestOffset(localPosition);
		var sample = Path.Curve.SampleBakedWithRotation(offset, false, true);
		return (Path.GlobalTransform.Basis * -sample.Basis.Z).Normalized();
	}
}
