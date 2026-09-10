using System;
using System.Reflection.Metadata.Ecma335;
using Godot;

public partial class RandomManiaSpecialStage : Node3D
{
    [Export]
    public Path3D Path;

    [Export]
    public float PathWidth;

    [Export]
    public int Difficulty;

    [Export]
    public uint Seed;

    [Export]
    public Node3D ObjectContainer;

    [Export]
    public Vector3 ItemOffset = new Vector3(0, 1, 0);

    [Export]
    public PackedScene RingPrefab;

    [Export]
    public PackedScene BlueSpherePrefab;

    [Export]
    public WorldEnvironment WorldEnvironment;

    [Export]
    public Material[] Skies = new Material[0];

    [Export]
    public int RingPity = 500;
    int RingPityCounter = 0;

    public override void _Ready()
    {
        RandomizePath();
        Generate();
    }

    void Generate()
    {
        float sectionLength = 100 / (Difficulty + 1);
        float sectionWidth = PathWidth / (Difficulty + 1);

        int skyMaterial = (Difficulty - 1) % Skies.Length;
        if (Seed == 0)
        {
            GD.Randomize();
        }
        else
        {
            GD.Seed(Seed);
        }
        foreach (var cam in PlayerCamera.Instances)
        {
            WorldEnvironment.Environment.Sky.SkyMaterial = Skies[skyMaterial];
        }

        for (float z = 0; z < Path.Curve.GetBakedLength(); z += (PathWidth / 10))
        {
            var randomNumberZ = GD.Randi() % 1000;
            var centerPosition = Path.GlobalTransform * (Path.Curve.SampleBaked(z) + ItemOffset);
            for (float x = -PathWidth / 2; x < PathWidth / 2; x += PathWidth / 10)
            {
                var randomNumberX = GD.Randi() % 100;
                if (randomNumberX < 10)
                {
                    randomNumberZ = GD.Randi() % 100;
                }

                var rows = GD.Randi() % 4 + 1;
                RingPityCounter++;
                if (RingPityCounter > RingPity)
                {
                    randomNumberZ = 0;
                    RingPityCounter = 0;
                }

                for (int r = 0; r < rows; r++)
                {
                    if (randomNumberZ < 1)
                    {
                        var newObject = RingPrefab.Instantiate<Node3D>();
                        ObjectContainer.AddChild(newObject);
                        newObject.GlobalPosition =
                            centerPosition
                            + Path.Curve.SampleBakedWithRotation(z).Basis.GetRotationQuaternion()
                                * new Vector3(x, 0, r * PathWidth / 10);

                        // Optimizer in StageData will enable it
                        newObject.ProcessMode = ProcessModeEnum.Disabled;
                        newObject.Visible = false;
                    }
                    else if (randomNumberZ + 0.01 * (Difficulty + 1) < 5)
                    {
                        var newObject = BlueSpherePrefab.Instantiate<Node3D>();
                        ObjectContainer.AddChild(newObject);
                        newObject.GlobalPosition =
                            centerPosition
                            + Path.Curve.SampleBakedWithRotation(z).Basis.GetRotationQuaternion()
                                * new Vector3(x, 0, r * PathWidth / 10);

                        // Optimizer in StageData will enable it
                        newObject.ProcessMode = ProcessModeEnum.Disabled;
                        newObject.Visible = false;
                    }
                }
            }
        }
    }

    void RandomizePath()
    {
        float radius = 500 + 25 * Difficulty;
        float difficultyVariance = 10 * Difficulty;
        int points = 10 * (Difficulty + 1);
        Path.Curve.ClearPoints();

        if (Seed == 0)
        {
            GD.Randomize();
        }
        else
        {
            GD.Seed(Seed);
        }

        var dir = GD.Randf() >= 0.5f ? 1 : -1;

        for (int i = 0; i < points; i++)
        {
            var x = Mathf.Sin(dir * 2 * Mathf.Pi * (float)i / points);
            var z = Mathf.Cos(dir * 2 * Mathf.Pi * (float)i / points);

            float variance = 2 * ((float)GD.Randf() - 0.5f) * difficultyVariance;
            if (i == 0 || i == points - 1)
            {
                variance = 0;
            }

            Path.Curve.AddPoint(new Vector3(x * (radius + variance), 0, z * (radius + variance)));
        }

        foreach (var player in PlayerController.Instances)
        {
            var start = Path.Curve.SampleBakedWithRotation(0);
            player.GlobalPosition = start.Origin + Vector3.Up * 2;
            player.GlobalRotation = start.Basis.GetRotationQuaternion().GetEuler();

            player.LinearVelocity = start.Basis.GetRotationQuaternion() * Vector3.Forward;
        }
    }
}
