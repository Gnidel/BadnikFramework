using System;
using Godot;

public partial class Pole : Node3D
{
    [Export]
    public Grabbable Grabbable;

    [Export]
    public Node3D RotatingNode;

    [Export]
    public Vector3 RotationAxis = new Vector3(0, 0, 1);

    [Export]
    public float RotationSpeedDegrees = 360f * 2f;

    [Export]
    public Vector2 SuccessRange = new Vector2(-15f, 135f);

    [Export]
    public Vector3 SuccessEjectVelocity = new Vector3(50, 50, 0);

    [Export]
    public Vector3 FailEjectVelocity = new Vector3(0, -10, 0);

    [Export]
    public Node3D GuideSuccess;

    [Export]
    public Node3D GuideFail;

    [Export]
    public AudioStreamPlayer3D AudioPlayer;

    [Export]
    public AudioStream SuccessRangeStartSoundEffect;

    [Export]
    public AudioStream SuccessShootSoundEffect;

    bool isActive = false;
    float currentRotationDeg = 0;

    public override void _Ready()
    {
        Grabbable.OnPlayerGrab += this.OnPlayerGrab;
        Grabbable.OnPlayerDrop += this.OnPlayerDrop;

        RotatingNode.Rotation = Vector3.Zero;
        currentRotationDeg = 0;

        GuideSuccess.Visible = false;
        GuideFail.Visible = false;
    }

    public void OnPlayerGrab(PlayerController player)
    {
        isActive = true;
    }

    public void OnPlayerDrop(PlayerController player)
    {
        isActive = false;

        bool success =
            currentRotationDeg >= SuccessRange.X && currentRotationDeg <= SuccessRange.Y
            || (currentRotationDeg - 360) >= SuccessRange.X
                && (currentRotationDeg) <= SuccessRange.Y
            || (currentRotationDeg) >= SuccessRange.X
                && (currentRotationDeg + 360) <= SuccessRange.Y; // It may look dumb but it allows using negative degrees for limits
        if (success)
        {
            player.LinearVelocity = this.GlobalBasis.GetRotationQuaternion() * SuccessEjectVelocity;

            AudioPlayer.Stream = SuccessShootSoundEffect;
            AudioPlayer.Play();
        }
        else
        {
            player.LinearVelocity = this.GlobalBasis.GetRotationQuaternion() * FailEjectVelocity;
        }

        RotatingNode.Rotation = Vector3.Zero;
        currentRotationDeg = 0;

        GuideSuccess.Visible = false;
        GuideFail.Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!isActive)
            return;

        var prevPotentialSuccess =
            currentRotationDeg >= SuccessRange.X && currentRotationDeg <= SuccessRange.Y
            || (currentRotationDeg - 360) >= SuccessRange.X
                && (currentRotationDeg) <= SuccessRange.Y
            || (currentRotationDeg) >= SuccessRange.X
                && (currentRotationDeg + 360) <= SuccessRange.Y; // It may look dumb but it allows using negative degrees for limits

        RotatingNode.RotateObjectLocal(
            RotationAxis,
            Mathf.DegToRad(RotationSpeedDegrees) * (float)delta
        );

        currentRotationDeg += RotationSpeedDegrees * (float)delta;
        if (currentRotationDeg >= 360)
        {
            currentRotationDeg -= 360;
        }
        if (currentRotationDeg < 0)
        {
            currentRotationDeg += 360;
        }

        var potentialSuccess =
            currentRotationDeg >= SuccessRange.X && currentRotationDeg <= SuccessRange.Y
            || (currentRotationDeg - 360) >= SuccessRange.X
                && (currentRotationDeg) <= SuccessRange.Y
            || (currentRotationDeg) >= SuccessRange.X
                && (currentRotationDeg + 360) <= SuccessRange.Y; // It may look dumb but it allows using negative degrees for limits
        GuideSuccess.Visible = potentialSuccess;
        GuideFail.Visible = !potentialSuccess;

        if (!prevPotentialSuccess && potentialSuccess)
        {
            AudioPlayer.Stream = SuccessRangeStartSoundEffect;
            AudioPlayer.Play();

            if (Grabbable.currentPlayer.Player.NpcPartnerControl.IsNpc)
            {
                Grabbable.currentPlayer.Player.NpcPartnerControl.PlayerInput.NpcPressButton(
                    "actionjump"
                );
            }
        }
    }
}
