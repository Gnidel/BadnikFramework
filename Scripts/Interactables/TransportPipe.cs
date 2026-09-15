using System.Collections.Generic;
using Godot;

public partial class TransportPipe : Path3D
{
    private const string HomingTargetScenePath =
        "res://SonicAssets/Prefabs/Misc/homing_target.tscn";

    [Export]
    public float Speed = 50f;

    [Export]
    public bool Bidirectional = false;

    [Export]
    public float MinimumExitSpeed = 0f;

    [Export]
    public float MaximumExitSpeed = 0f;

    [Export]
    public float ReentryCooldown = 0.5f;

    [Export]
    public float EntryRadius = 2f;

    [Export]
    public bool HomingAttackTarget = true;

    [Signal]
    public delegate void PlayerEnteredEventHandler(PlayerController player);

    [Signal]
    public delegate void PlayerExitedEventHandler(PlayerController player);

    private sealed class PlayerState
    {
        public PlayerController Player;
        public ActionAutoGimmick AutoGimmick;
        public PathFollow3D Follower;
        public float Direction;
        public float EntrySpeed;
    }

    private readonly Dictionary<PlayerController, PlayerState> players = new();
    private readonly Dictionary<PlayerController, double> cooldowns = new();
    private readonly HashSet<PlayerController> endpointSuppressedPlayers = new();

    public override void _Ready()
    {
        if (Curve == null || Curve.GetPointCount() < 2)
            return;

        var startArea = CreateEndpointArea("StartArea", 0f);
        startArea.BodyEntered += body => OnEndpointBodyEntered(body, 1f);
        startArea.BodyExited += OnEndpointBodyExited;
        AddHomingTarget(startArea);

        if (Bidirectional)
        {
            var endArea = CreateEndpointArea("EndArea", Curve.GetBakedLength());
            endArea.BodyEntered += body => OnEndpointBodyEntered(body, -1f);
            endArea.BodyExited += OnEndpointBodyExited;
            AddHomingTarget(endArea);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var cooldownCopy = new List<PlayerController>(cooldowns.Keys);
        foreach (var player in cooldownCopy)
        {
            cooldowns[player] -= delta;
            if (cooldowns[player] <= 0)
                cooldowns.Remove(player);
        }

        var completed = new List<PlayerState>();
        foreach (var state in players.Values)
        {
            state.Follower.Progress += state.Direction * Speed * (float)delta;
            ApplyPlayerTransform(state);
            if (state.Direction > 0 && state.Follower.ProgressRatio >= 1)
                completed.Add(state);
            else if (state.Direction < 0 && state.Follower.ProgressRatio <= 0)
                completed.Add(state);
        }

        foreach (var state in completed)
            Exit(state);
    }

    private Area3D CreateEndpointArea(string name, float progress)
    {
        var area = new Area3D { Name = name, CollisionMask = 2 };
        var shape = new CollisionShape3D { Shape = new SphereShape3D { Radius = EntryRadius } };
        area.Position = Curve.SampleBaked(progress);
        AddChild(area);
        area.AddChild(shape);
        return area;
    }

    private void AddHomingTarget(Area3D endpointArea)
    {
        if (!HomingAttackTarget)
            return;

        var scene = ResourceLoader.Load<PackedScene>(HomingTargetScenePath);
        var target = scene?.Instantiate<HomingTarget>();
        if (target != null)
            endpointArea.AddChild(target);
    }

    private void OnEndpointBodyEntered(Node3D body, float direction)
    {
        var player = body.GetNodeOrNull<PlayerController>(".");
        if (
            player == null
            || players.ContainsKey(player)
            || cooldowns.ContainsKey(player)
            || endpointSuppressedPlayers.Contains(player)
        )
            return;

        var action = player.GetNodeOrNull<ActionAutoGimmick>(
            "./PlayerControl/Actions/ActionAutoGimmick"
        );
        if (action == null)
            return;

        var homing = player.GetNodeOrNull<ActionHoming>("./PlayerControl/Actions/ActionHoming");
        var entrySpeed = homing != null && homing.IsHoming
            ? homing.sonicGTHomingAttackSpeed
            : player.LinearVelocity.Length();

        var follower = new PathFollow3D
        {
            RotationMode = PathFollow3D.RotationModeEnum.Oriented,
            Loop = false
        };
        AddChild(follower);
        follower.Progress = direction > 0 ? 0f : Curve.GetBakedLength();

        var state = new PlayerState
        {
            Player = player,
            AutoGimmick = action,
            Follower = follower,
            Direction = direction,
            EntrySpeed = entrySpeed
        };
        players.Add(player, state);
        ApplyPlayerTransform(state);
        action.StartAutoGimmick("RollingNotransition", follower);
        EmitSignal(SignalName.PlayerEntered, player);
    }

    private void Exit(PlayerState state)
    {
        var exitDirection = GetExitDirection(state.Direction);
        var speed = Mathf.Max(state.EntrySpeed, MinimumExitSpeed);
        if (MaximumExitSpeed > 0)
            speed = Mathf.Min(speed, MaximumExitSpeed);

        ApplyPlayerTransform(state);
        state.AutoGimmick.EndAutoGimmick("Air");
        state.Player.LinearVelocity = exitDirection * speed;
        state.Player.TimedLerpedRotation = 0.5f;

        players.Remove(state.Player);
        cooldowns[state.Player] = ReentryCooldown;
        endpointSuppressedPlayers.Add(state.Player);
        state.Follower.QueueFree();
        EmitSignal(SignalName.PlayerExited, state.Player);
    }

    private void ApplyPlayerTransform(PlayerState state)
    {
        state.Player.GlobalTransform = state.Follower.GlobalTransform;
        if (state.Direction < 0)
            state.Player.RotateY(Mathf.Pi);
    }

    private void OnEndpointBodyExited(Node3D body)
    {
        var player = body.GetNodeOrNull<PlayerController>(".");
        if (player != null)
            endpointSuppressedPlayers.Remove(player);
    }

    private Vector3 GetExitDirection(float travelDirection)
    {
        var length = Curve.GetBakedLength();
        var offset = Mathf.Min(0.05f, length * 0.25f);
        var end = travelDirection > 0 ? length : 0f;
        var before = Curve.SampleBaked(end - travelDirection * offset);
        var after = Curve.SampleBaked(end);
        var direction = (after - before).Normalized();
        return GlobalBasis * direction;
    }
}