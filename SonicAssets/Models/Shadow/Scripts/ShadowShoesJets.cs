using System;
using System.Linq;
using Godot;

public partial class ShadowShoesJets : Node3D
{
    [Export]
    public Sprite3D[] Sprites = new Sprite3D[0];

    [Export]
    public Vector2 SpritesSizeLimits = new Vector2(0, 0.002f);

    [Export]
    public OmniLight3D[] Lights = new OmniLight3D[0];

    [Export]
    public Vector2 LightsEnergyLimits = new Vector2(0, 1f);

    [Export]
    public AnimationTree AnimationTree;
    private const string SpeedBlendPropertyPath = "parameters/Air/velocityXZ/blend_amount";

    [Export]
    public string[] JetActiveStates = { "Running", "Air" };

    [Export]
    public float JetSizeChangeSpeed = 10f;

    public override void _PhysicsProcess(double delta)
    {
        var speedBlend = AnimationTree.Get(SpeedBlendPropertyPath).AsDouble();
        var stateMachine = AnimationTree
            .Get("parameters/playback")
            .As<AnimationNodeStateMachinePlayback>();
        var currentAnim = stateMachine.GetCurrentNode().ToString();

        var targetSize = Mathf.Lerp(SpritesSizeLimits.X, SpritesSizeLimits.Y, (float)speedBlend);
        if (!JetActiveStates.Contains(currentAnim))
        {
            targetSize = 0;
        }
        foreach (var sprite in Sprites)
        {
            sprite.PixelSize = Mathf.Lerp(
                sprite.PixelSize,
                targetSize,
                (float)delta * JetSizeChangeSpeed
            );
        }

        var targetEnergy = Mathf.Lerp(
            LightsEnergyLimits.X,
            LightsEnergyLimits.Y,
            (float)speedBlend
        );
        if (!JetActiveStates.Contains(currentAnim))
        {
            targetEnergy = 0;
        }
        foreach (var light in Lights)
        {
            light.LightEnergy = Mathf.Lerp(
                light.LightEnergy,
                targetEnergy,
                (float)delta * JetSizeChangeSpeed
            );
        }
    }
}
