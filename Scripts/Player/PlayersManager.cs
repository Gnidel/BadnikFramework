using System;
using Godot;
using Godot.Collections;

public partial class PlayersManager : Node3D
{
    // List of players
    public static Array<PackedScene> PlayablePlayers;
    public static Array<string> PlayerIdentifiers; // Defines inputs
    public static Array<PackedScene> NpcPlayers;

    [Export]
    public Array<PackedScene> DefaultPlayablePlayers;

    [Export]
    public Array<string> DefaultPlayerIdentifiers; // Defines inputs

    [Export]
    public Array<PackedScene> DefaultNpcPlayers;

    // Player UI
    [Export]
    public PlayerUI PlayerUI;

    //[Export] public SubViewportContainer FirstViewportContainerPrefab;
    [Export]
    public PackedScene ViewportContainerPrefab;

    // Containers
    [Export]
    public Node3D PlayersContainer;

    [Export]
    public GridContainer SubviewportContainer;

    // Gizmos
    [Export]
    public Node3D StartPointMesh;

    [Export]
    public bool CharacterSwappingEnabled = false;

    [Signal]
    public delegate void OnAfterSpawningDoneEventHandler();

    public static PlayersManager Instance;

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            GD.PrintErr("Multiple PlayersManagers! The most recent one will self-destruct.");
            this.QueueFree();
            return;
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!this.CharacterSwappingEnabled)
            return;
        foreach (var player in PlayerController.Instances)
        {
            if (player.NpcPartnerControl.IsNpc)
                continue;
            if (player.PlayerInput.IsActionPressed(@event, "actioncharaswap_l"))
            {
                SwapCharacters(player.PlayerID, -1);
                return;
            }
            if (player.PlayerInput.IsActionPressed(@event, "actioncharaswap_r"))
            {
                SwapCharacters(player.PlayerID, +1);
                return;
            }
        }
    }

    public override void _Ready()
    {
        if (StartPointMesh != null)
        {
            StartPointMesh.QueueFree();
        }

        if (PlayablePlayers == null || PlayablePlayers.Count < 1)
        {
            PlayablePlayers = DefaultPlayablePlayers;
            PlayerIdentifiers = DefaultPlayerIdentifiers;
            NpcPlayers = DefaultNpcPlayers;
        }

        if (PlayablePlayers.Count <= 2)
        {
            SubviewportContainer.Columns = 1;
        }
        else
        {
            SubviewportContainer.Columns = (PlayablePlayers.Count + 1) / 2;
        }

        // Adding real players
        PlayerController player1 = null;
        for (int i = 0; i < PlayablePlayers.Count; i++)
        {
            var player = PlayablePlayers[i].Instantiate<PlayerController>();
            player.PlayerInput.PlayerIdentifier = PlayerIdentifiers[i];
            player.PlayerInput.InitializeInputType();
            PlayersContainer.AddChild(player);
            player.TopLevel = true;
            player.GlobalRotation = this.GlobalRotation;

            if (i == 0)
            {
                player1 = player;
            }

            var newSubviewport = ViewportContainerPrefab.Instantiate<SubViewportContainer>(); //FirstViewportContainerPrefab.Duplicate();
            var camera = newSubviewport.GetNodeOrNull<PlayerCamera>("./SubViewport/Camera3D");
            if (camera != null)
            {
                camera.PlayerID = player.PlayerID;
            }

            var playerUI = newSubviewport.GetNodeOrNull<OnePlayerUI>("./SubViewport/PlayerUI");
            if (playerUI != null)
            {
                playerUI.PlayerID = player.PlayerID;
                playerUI.HomingIcon = playerUI.GetNode<Node2D>("./HomingIcon");
            }

            SubviewportContainer.AddChild(newSubviewport);
        }

        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.GlobalRotation = this.GlobalRotation;
        }

        // Adding NPC players
        for (int i = 0; i < NpcPlayers.Count; i++)
        {
            var player = NpcPlayers[i].Instantiate<PlayerController>();
            player.PlayerInput.PlayerIdentifier = "npc" + i.ToString();
            player.PlayerInput.ReadRealInput = false;
            var npcControl = player.GetNodeOrNull<NpcPartnerControl>("./PlayerControl/NpcControl");
            if (npcControl != null)
            {
                npcControl.IsNpc = true;
                npcControl.Target = player1;
            }
            PlayersContainer.AddChild(player);
            player.TopLevel = true;
            player.GlobalRotation = this.GlobalRotation;
            player.Position += player.Basis.X * (i / 2 + 1) * (i % 2 == 0 ? 1 : -1) * 5;
        }

        GetTree().Paused = false;
        EmitSignal(SignalName.OnAfterSpawningDone);
    }

    // NOTE: This is designed only around offset == -1 or == +1.
    public int GetNextCharacter(int playerID, int offset)
    {
        if (offset != -1 && offset != 1)
        {
            throw new InvalidOperationException("GetNextCharacter offset should be +1 or -1");
        }
        int i = playerID;
        while (true)
        {
            i = i + offset;
            if (i >= PlayerController.Instances.Count)
            {
                i = 0;
            }
            else if (i < 0)
            {
                i = PlayerController.Instances.Count - 1;
            }
            if (PlayerController.Instances[i].NpcPartnerControl.IsNpc || i == playerID)
            {
                return i;
            }
        }
    }

    public string GetNextCharacterName(int playerID, int offset)
    {
        int nextCharacter = GetNextCharacter(playerID, offset);
        return PlayerController.Instances[nextCharacter].CharacterName;
    }

    void SwapCharacters(int currentPlayerIdx, int offset)
    {
        if (!CharacterSwappingEnabled)
            return;
        if (offset == 0)
            return;
        if (PlayerDamage.AlivePlayersCount() == 0)
            return;
        // TODO suggestion: Make teams. Currently players can take over remaining NPCs with assumption that they are on the same team.
        
        int nextPlayerIdx = GetNextCharacter(currentPlayerIdx, offset);

        var currentPlayer = PlayerController.Instances[currentPlayerIdx];
        var nextPlayer = PlayerController.Instances[nextPlayerIdx];
        var swapTransform = currentPlayer.GlobalTransform;
        var swapVelocity = currentPlayer.LinearVelocity;

        // Swap camera
        var cam = PlayerCamera.Instances[currentPlayer.PlayerID];
        cam.PlayerID = nextPlayer.PlayerID;
        cam.CameraTargets.Remove(currentPlayer);
        cam.CameraTargets.Add(nextPlayer);
        cam.Player = nextPlayer;

        PlayerCamera.Instances.Remove(currentPlayer.PlayerID);
        PlayerCamera.Instances.Add(nextPlayer.PlayerID, cam);

        // Swap inputs
        currentPlayer.NpcPartnerControl.IsNpc = true;
        currentPlayer.NpcPartnerControl.Target = nextPlayer;
        currentPlayer.PlayerInput.ReadRealInput = false;

        nextPlayer.NpcPartnerControl.IsNpc = false;
        nextPlayer.PlayerInput.PlayerIdentifier = currentPlayer.PlayerInput.PlayerIdentifier;
        nextPlayer.PlayerInput.ReadRealInput = true;

        // Swap UI
        var onePlayerUI = OnePlayerUI.Instances[currentPlayer.PlayerID];
        OnePlayerUI.Instances.Remove(currentPlayer.PlayerID);
        OnePlayerUI.Instances.Add(nextPlayer.PlayerID, onePlayerUI);

        onePlayerUI.PlayerID = nextPlayer.PlayerID;
        onePlayerUI.OnCharacterSwap();

        // Transfer rings
        string[] itemsToTransfer =
        {
            "rings",
            "redring1",
            "redring2",
            "redring3",
            "redring4",
            "redring5",
            "chaosemerald_blue",
            "chaosemerald_cyan",
            "chaosemerald_gray",
            "chaosemerald_green",
            "chaosemerald_magenta",
            "chaosemerald_red",
            "chaosemerald_yellow",
        };
        foreach (var itemToTransfer in itemsToTransfer)
        {
            var count = currentPlayer.PlayerInventory.GetItemCount(itemToTransfer);
            currentPlayer.PlayerInventory.SetItemCount(itemToTransfer, 0);
            nextPlayer.PlayerInventory.AddItemCount(itemToTransfer, count, false);
        }

        // Transfer spline locks
        SplineMovement currentSplineMovement = currentPlayer.GetNodeOrNull<SplineMovement>(
            "./PlayerControl/SplineMovement"
        );
        SplineMovement nextSplineMovement = nextPlayer.GetNodeOrNull<SplineMovement>(
            "./PlayerControl/SplineMovement"
        );
        if (currentSplineMovement != null && nextSplineMovement != null)
        {
            nextSplineMovement.CopyStateFrom(currentSplineMovement);
            nextPlayer.PlayerInput.TwodimensionalMode = currentPlayer.PlayerInput.TwodimensionalMode;
            nextPlayer.PlayerInput.Plane2DControlsX = currentPlayer.PlayerInput.Plane2DControlsX;
            nextPlayer.PlayerInput.Plane2DControlsY = currentPlayer.PlayerInput.Plane2DControlsY;
        }

        var currentActionGrind = currentPlayer.GetNodeOrNull<ActionGrind>(
            "./PlayerControl/Actions/ActionGrind"
        );
        var nextActionGrind = nextPlayer.GetNodeOrNull<ActionGrind>(
            "./PlayerControl/Actions/ActionGrind"
        );
        if (currentActionGrind != null && nextActionGrind != null)
        {
            nextActionGrind.CopyStateFrom(currentActionGrind);
        }

        // Teleport

        nextPlayer.ProcessMode = ProcessModeEnum.Disabled; // Wait a frame, otherwise the player will ignore teleportation

        nextPlayer.GlobalTransform = swapTransform;
        nextPlayer.LinearVelocity = swapVelocity;
        RestoreProcessMode(nextPlayer, swapTransform, swapVelocity);
    }

    async void RestoreProcessMode(
        PlayerController player,
        Transform3D swapTransform,
        Vector3 swapVelocity
    )
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        player.GlobalTransform = swapTransform;
        player.LinearVelocity = swapVelocity;
        player.ProcessMode = ProcessModeEnum.Inherit;
    }
}
