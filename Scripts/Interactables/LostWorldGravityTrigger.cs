using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Godot.Collections;

public partial class LostWorldGravityTrigger : Area3D
{
    // TODO: Known bugs: Trigger refreshes during pause. It doesn't seem to do anything wrong with the movement, but looks silly during pause.
    // TODO: Refactor this. Too much copypasta.

    public enum GravityMode
    {
        TOWARDS,
        AGAINST,
        UPVECTOR,
        LAST_STEP,
        CONST_DIR,
    }

    [Export]
    public Path3D Path;

    [Export]
    public Vector3 ConstDir = -Vector3.Up;

    public Array<PlayerController> Players = new Array<PlayerController>();
    public Array<EnemyControl> Enemies = new Array<EnemyControl>();

    [Export]
    public GravityMode Mode = GravityMode.TOWARDS;

    [Export]
    public bool IgnoreExit = false;

    public static List<LostWorldGravityTrigger> Instances = new List<LostWorldGravityTrigger>();

    public override void _EnterTree()
    {
        Instances.Add(this);
    }

    public override void _ExitTree()
    {
        Instances.Remove(this);
    }

    public override void _Ready()
    {
        if (GetSignalConnectionList("body_entered").Count == 0)
        {
            this.BodyEntered += this.OnAreaEnter;
        }
        if (GetSignalConnectionList("body_exited").Count == 0)
        {
            this.BodyExited += this.OnAreaExit;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // TODO: Refactor this code to reduce copypasta between enemies and players.
        if (Players.Remove(null))
            return;
        foreach (var Player in Players)
        {
            if (Player != null)
            {
                Vector3 offset = Vector3.Zero;
                if (this.Mode != GravityMode.CONST_DIR && this.Mode != GravityMode.LAST_STEP)
                {
                    Vector3 nearestPoint = Path.Curve.GetClosestPoint(
                        Path.GlobalTransform.AffineInverse() * Player.GlobalPosition
                    ); // Curve wants local space.
                    offset = Path.GlobalTransform * (nearestPoint) - Player.GlobalPosition;
                }

                var newGravityNormalized = Player.Gravity.Normalized();
                switch (Mode)
                {
                    case GravityMode.TOWARDS:
                        newGravityNormalized = offset.Normalized();
                        break;
                    case GravityMode.AGAINST:
                        newGravityNormalized = -offset.Normalized();
                        break;
                    case GravityMode.UPVECTOR:
                        var nearestOffset = Path.Curve.GetClosestOffset(
                            Path.GlobalTransform.AffineInverse() * Player.GlobalPosition
                        );
                        var upVector = Path.Curve.SampleBakedUpVector(nearestOffset);
                        newGravityNormalized = Path.GlobalTransform * upVector;
                        break;
                    case GravityMode.LAST_STEP:
                        newGravityNormalized = -Player.GroundNormal.Normalized();
                        break;
                    case GravityMode.CONST_DIR:
                        GD.PrintErr(
                            "Const dir but player still registered in Lost World gravity trigger. This should never happen."
                        );
                        break;
                }

                var gravityRotation = new Quaternion(
                    Player.Gravity.Normalized(),
                    newGravityNormalized
                ).Normalized();
                Player.LinearVelocity = gravityRotation * Player.LinearVelocity;

                Player.Gravity = newGravityNormalized * Player.Gravity.Length();
            }
        }

        for (int i = 0; i < Enemies.Count; i++)
        {
            if (Enemies[i].HP <= 0)
            {
                Enemies.RemoveAt(i);
                // One frame of delay won't hurt. There may be more of them, so do it on the next frames.
                return;
            }
        }
        foreach (var Enemy in Enemies)
        {
            if (Enemy != null || !Enemy.Gravity.IsZeroApprox())
            {
                var nearestPoint = Path.Curve.GetClosestPoint(
                    Path.GlobalTransform.AffineInverse() * Enemy.RigidBody.GlobalPosition
                ); // Curve wants local space.
                var offset = Path.GlobalTransform * (nearestPoint) - Enemy.RigidBody.GlobalPosition;
                if (offset.IsZeroApprox())
                {
                    continue;
                }

                var newGravityNormalized = Enemy.Gravity.Normalized();
                switch (Mode)
                {
                    case GravityMode.TOWARDS:
                        newGravityNormalized = offset.Normalized();
                        break;
                    case GravityMode.AGAINST:
                        newGravityNormalized = -offset.Normalized();
                        break;
                    case GravityMode.UPVECTOR:
                        var nearestOffset = Path.Curve.GetClosestOffset(
                            Path.GlobalTransform.AffineInverse() * Enemy.RigidBody.GlobalPosition
                        );
                        var upVector = Path.Curve.SampleBakedUpVector(nearestOffset);
                        newGravityNormalized = Path.GlobalTransform * upVector;
                        break;
                    case GravityMode.LAST_STEP:
                        var enemyGroundNormal = -Enemy.Gravity.Normalized(); // TODO: WILL NOT WORK BECAUSE ENEMIES DON'T DETECT GROUND NORMAL! Do something about it?
                        newGravityNormalized = enemyGroundNormal;
                        break;

                    case GravityMode.CONST_DIR:
                        GD.PrintErr(
                            "Const dir but enemy still registered in Lost World gravity trigger. This should never happen."
                        );
                        break;
                }

                var gravityRotation = new Quaternion(
                    Enemy.Gravity.Normalized(),
                    newGravityNormalized
                );
                Enemy.RigidBody.LinearVelocity =
                    gravityRotation.Normalized() * Enemy.RigidBody.LinearVelocity;

                Enemy.Gravity = newGravityNormalized * Enemy.Gravity.Length();
            }
        }
    }

    public void OnAreaEnter(Node3D other)
    {
        // TODO: Reduce copypasta between players and enemies
        var Player = other.GetNodeOrNull<PlayerController>(".");
        if (Player != null && !Players.Contains(Player))
        {
            // Remove player from other areas of gravity influence. Player can only be in one gravity field.
            foreach (var lwtrigger in Instances)
            {
                lwtrigger.Players.Remove(Player);
            }

            if (Mode != GravityMode.CONST_DIR)
            {
                Players.Add(Player);
            }

            Vector3 offset = Vector3.Zero;
            if (this.Mode != GravityMode.CONST_DIR && this.Mode != GravityMode.LAST_STEP)
            {
                Vector3 nearestPoint = Path.Curve.GetClosestPoint(
                    Path.GlobalTransform.AffineInverse() * Player.GlobalPosition
                ); // Curve wants local space.
                offset = Path.GlobalTransform * (nearestPoint) - Player.GlobalPosition;
            }

            switch (Mode)
            {
                case GravityMode.TOWARDS:
                    Player.Gravity = offset.Normalized() * Player.Gravity.Length();
                    break;
                case GravityMode.AGAINST:
                    Player.Gravity = -offset.Normalized() * Player.Gravity.Length();
                    break;
                case GravityMode.UPVECTOR:
                    var nearestOffset = Path.Curve.GetClosestOffset(
                        Path.GlobalTransform.AffineInverse() * Player.GlobalPosition
                    );
                    var upVector = Path.Curve.SampleBakedUpVector(nearestOffset);
                    break;
                case GravityMode.LAST_STEP:
                    Player.Gravity = -Player.GroundNormal.Normalized() * Player.Gravity.Length();
                    break;
                case GravityMode.CONST_DIR:
                    Player.Gravity = ConstDir.Normalized() * Player.Gravity.Length();
                    break;
            }
        }

        var Enemy = other.GetNodeOrNull<EnemyControl>("./EnemyControl");
        if (Enemy != null && !Enemies.Contains(Enemy))
        {
            if (Enemy.IgnoreGravityTriggers || Enemy.Gravity.IsZeroApprox())
                return;
            // Remove enemy from other areas of gravity influence. Enemies can only be in one gravity field.
            foreach (var lwtrigger in Instances)
            {
                lwtrigger.Enemies.Remove(Enemy);
            }

            if (Mode != GravityMode.CONST_DIR)
            {
                Enemies.Add(Enemy);
            }

            Vector3 offset = Vector3.Zero;
            if (this.Mode != GravityMode.CONST_DIR)
            {
                var nearestPoint = Path.Curve.GetClosestPoint(
                    Path.GlobalTransform.AffineInverse() * Enemy.RigidBody.GlobalPosition
                ); // Curve wants local space.
                offset = Path.GlobalTransform * (nearestPoint) - Enemy.RigidBody.GlobalPosition;
            }

            switch (Mode)
            {
                case GravityMode.TOWARDS:
                    Enemy.Gravity = offset.Normalized() * Enemy.Gravity.Length();
                    break;
                case GravityMode.AGAINST:
                    Enemy.Gravity = -offset.Normalized() * Enemy.Gravity.Length();
                    break;
                case GravityMode.UPVECTOR:
                    var nearestOffset = Path.Curve.GetClosestOffset(
                        Path.GlobalTransform.AffineInverse() * Enemy.RigidBody.GlobalPosition
                    );
                    var upVector = Path.Curve.SampleBakedUpVector(nearestOffset);
                    break;
                case GravityMode.LAST_STEP:
                    // TODO: Will not work because enemies don't detect ground normal.
                    //Enemy.Gravity = -Enemy.GroundNormal.Normalized() * Enemy.Gravity.Length();
                    break;
                case GravityMode.CONST_DIR:
                    Enemy.Gravity = ConstDir.Normalized() * Enemy.Gravity.Length();
                    break;
            }
        }
    }

    public void OnAreaExit(Node3D other)
    {
        // TODO: Reduce copypasta between players and enemies

        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player != null)
        {
            if (!IgnoreExit)
            {
                player.Gravity = -Vector3.Up * player.Gravity.Length();
                Players.Remove(player);
            }
        }

        var enemy = other.GetNodeOrNull<EnemyControl>("./EnemyControl");
        if (enemy != null && enemy.IgnoreGravityTriggers)
        {
            if (!IgnoreExit)
            {
                enemy.Gravity = -Vector3.Up * enemy.Gravity.Length();
                Enemies.Remove(enemy);
            }
        }
    }
}
