using System;
using System.Collections.Generic;
using Godot;

public partial class CollectableItem : Node3D
{
    [Export]
    public Curve AttractionSpeed;

    [Export]
    public float maxAttractionTime = 1;

    [Export]
    public AudioStream SoundEffect;

    [Export]
    public string ItemName;

    [Export]
    public int ItemCount;

    [Export]
    public bool IsAdditive = true;

    [Export]
    public GpuParticles3D particles;

    [Export]
    public Node3D modelNode;
    public double AttractionTimer;
    public Node3D AttractiveNode;
    private bool collected = false;

    [Export]
    public bool DestroyParentOnDestroy = false;

    [Export]
    public bool DontCull = false;

    [Export]
    public double CanBeAttractedAfter = 2f;

    [Export]
    public double TemporaryExistenceTime = 0;
    private double existenceTimer = 0f;

    public static List<CollectableItem> Instances = new List<CollectableItem>();

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
        AttractionTimer = 0;
    }

    public override void _Process(double delta)
    {
        existenceTimer += delta;
        if (existenceTimer > CanBeAttractedAfter && Mathf.IsZeroApprox(TemporaryExistenceTime))
            return;

        if (!Mathf.IsZeroApprox(TemporaryExistenceTime))
        {
            if (existenceTimer > TemporaryExistenceTime)
            {
                if (DestroyParentOnDestroy)
                {
                    this.GetParent().QueueFree();
                }
                else
                {
                    this.QueueFree();
                }
            }
            else if (existenceTimer + 2 > TemporaryExistenceTime && IsInstanceValid(modelNode))
            {
                modelNode.Visible = !modelNode.Visible;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (
            AttractiveNode != null
            && IsInstanceValid(AttractiveNode)
            && AttractiveNode.IsInsideTree()
        )
        {
            AttractionTimer += delta;
            var currentAttractionSpeed = AttractionSpeed.Sample(
                (float)AttractionTimer / maxAttractionTime
            );
            this.GlobalPosition = this.GlobalPosition.Lerp(
                AttractiveNode.GlobalPosition + AttractiveNode.GlobalBasis.Y * 0.5f,
                currentAttractionSpeed * (float)delta
            );
        }
    }

    public void OnPickup(Node3D other)
    {
        var player = other.GetNodeOrNull<PlayerController>(".");
        if (player == null)
            return;

        PlayerInventory inventory = null;
        var npcControl = other.GetNodeOrNull<NpcPartnerControl>("./PlayerControl/NpcControl");
        if (npcControl != null && npcControl.IsNpc)
        {
            var targetPlayer = player.NpcPartnerControl.Target.GetNodeOrNull<PlayerController>(".");
            if (targetPlayer != null)
            {
                inventory = targetPlayer.PlayerInventory;
            }
        }
        else
        {
            inventory = other.GetNodeOrNull<PlayerInventory>(new NodePath("./Inventory"));
        }
        if (inventory == null)
            return;

        if (collected)
            return;
        collected = true;

        if (IsAdditive)
        {
            inventory.AddItemCount(ItemName, ItemCount, true);
        }
        else
        {
            inventory.SetItemCount(ItemName, ItemCount, true);
        }

        var audioPlayer = other.GetNodeOrNull<AudioStreamPlayer3D>(
            new NodePath("./Audio/InteractionsSoundEffectsPlayer")
        );
        if (audioPlayer != null)
        {
            audioPlayer.Stream = SoundEffect;
            audioPlayer.Play();
        }
        if (particles != null)
        {
            particles.Restart();
        }

        modelNode.Free();
        DestroyAfterTime();
        EmitSignal(SignalName.OnItemCollected, player);
    }

    public void OnAttract(Node3D other)
    {
        if (existenceTimer < CanBeAttractedAfter)
            return;

        var player = other.GetNodeOrNull<PlayerController>(".");

        if (player == null)
        {
            return;
        }
        if (player.NpcPartnerControl.IsNpc)
            return;

        var inventory = other.GetNodeOrNull<PlayerInventory>(new NodePath("./Inventory"));
        if (inventory != null)
        {
            AttractiveNode = other;
        }

        var rb = GetNodeOrNull<RigidBody3D>("..");
        if (rb != null)
        {
            rb.Freeze = true;
        }
    }

    private async void DestroyAfterTime()
    {
        this.SetPhysicsProcess(false);
        await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

        if (DestroyParentOnDestroy)
        {
            this.GetParent().QueueFree();
        }
        else
        {
            this.QueueFree();
        }
    }

    [Signal]
    public delegate void OnItemCollectedEventHandler(PlayerController collectorPlayer);
}
