using System;
using Godot;

public partial class ActionQuickStep : Node
{
    [Export]
    public PlayerController Player;

    [Export]
    public ActionGrind ActionGrind;

    [Export]
    public float QuickStepTime = 0.3f;

    [Export]
    public float QuickStepSpeed = 30;
    public float QuickStepDir = 0; // -1 is left, 1 is right, 0 is not quick stepping
    private float QuickStepTimer;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream RunQuickStepSound;

    [Export]
    public AudioStream IdleQuickStepSound;

    [Export]
    public GpuParticles3D ParticlesL;

    [Export]
    public GpuParticles3D ParticlesR;

    public override void _Ready()
    {
        if (ActionGrind == null)
        {
            ActionGrind = Player.GetNodeOrNull<ActionGrind>("./PlayerControl/Actions/ActionGrind");
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (
            QuickStepDir != 0
            || !Player.Grounded
            || Player.PlayerInput.TwodimensionalMode
            || ActionGrind.IsGrinding()
        )
            return;
        if (Player.PlayerInput.IsActionPressed(@event, "quickstep_l"))
        {
            QuickStepDir = -1;
            QuickStepTimer = 0;

            if (Player.VelocityXZ.Length() > 1)
            {
                Player.PlayerSkinController.TravelAnimation("RunQuickstepL");
                AudioPlayer.Stream = RunQuickStepSound;
                ParticlesL.Restart();
                ParticlesL.Emitting = true;
            }
            else
            {
                Player.PlayerSkinController.TravelAnimation("IdleQuickstepL");
                AudioPlayer.Stream = IdleQuickStepSound;
            }
            AudioPlayer.Play();
        }
        else if (Player.PlayerInput.IsActionPressed(@event, "quickstep_r"))
        {
            QuickStepDir = 1;
            QuickStepTimer = 0;

            if (Player.VelocityXZ.Length() > 1)
            {
                Player.PlayerSkinController.TravelAnimation("RunQuickstepR");
                AudioPlayer.Stream = RunQuickStepSound;
                ParticlesR.Restart();
                ParticlesR.Emitting = true;
            }
            else
            {
                Player.PlayerSkinController.TravelAnimation("IdleQuickstepR");
                AudioPlayer.Stream = IdleQuickStepSound;
            }
            AudioPlayer.Play();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (QuickStepDir == 0)
            return;
        QuickStepTimer += (float)delta;
        if (QuickStepTimer > QuickStepTime)
        {
            StopQuickStep();
            return;
        }
        Player.MoveAndCollide(Player.GlobalBasis.X * QuickStepSpeed * QuickStepDir * (float)delta);
    }

    void StopQuickStep()
    {
        QuickStepDir = 0;
        QuickStepTimer = 0;
        if (Player.VelocityXZ.Length() > 1)
        {
            Player.PlayerSkinController.TravelAnimation("Running");
        }
        else
        {
            Player.PlayerSkinController.TravelAnimation("Idle");
        }
    }
}
