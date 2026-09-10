using System;
using Godot;

public partial class BossEndCamera : Node
{
    [Export]
    public EnemyControl EnemyControl;

    [Export]
    public Node NextPhase;

    [Export]
    public AudioStreamPlayer3D SoundPlayer;

    [Export]
    public AudioStream CameraSound;

    [Export]
    public AudioStream BossExplosionSound;

    [Export]
    public Node3D[] CameraPoints;

    [Export]
    public float UnscaledSecondsPerCameraPoint = 1;
    private float CameraPointTimer = 0;

    [Export]
    public float TimeScale = 0.0001f;

    private int currentCameraPoint = -1;

    public override void _Ready()
    {
        this.ProcessMode = ProcessModeEnum.Disabled;
        currentCameraPoint = -1;
        CameraPointTimer = 0;
    }

    public override void _Process(double delta)
    {
        if (CameraPointTimer > UnscaledSecondsPerCameraPoint || currentCameraPoint == -1)
        {
            currentCameraPoint++;
            CameraPointTimer = 0;
            SoundPlayer.Stream = this.CameraSound;
            SoundPlayer.Play(0);
            Engine.TimeScale = this.TimeScale;
            if (currentCameraPoint == CameraPoints.Length && CameraPointTimer == 0)
            {
                Explode();
            }
            return;
        }
        if (currentCameraPoint >= CameraPoints.Length)
        {
            this.ProcessMode = ProcessModeEnum.Disabled;
            Engine.TimeScale = 1;
            foreach (var cam in PlayerCamera.Instances)
            {
                cam.Value.Point = null;
                cam.Value.Mode = PlayerCamera.CameraMode.Free;
            }
            ProceedToNextPhase();
            return;
        }
        CameraPointTimer += (float)delta / TimeScale;

        Node3D currentPoint = CameraPoints[currentCameraPoint];
        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.Point = currentPoint;
            cam.Value.Mode = PlayerCamera.CameraMode.Point;
            cam.Value.Fov = 75f;
        }
    }

    void Explode()
    {
        SoundPlayer.Stream = this.BossExplosionSound;
        SoundPlayer.Play(0);
        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.Filters.EnableImpactFrameFilter();
        }
    }

    void ProceedToNextPhase()
    {
        EnemyControl.BehaviorManager.SetPhase(NextPhase);
    }
}
