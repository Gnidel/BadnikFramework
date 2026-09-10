using System.Collections.Generic;
using Godot;

public partial class ActionSkate : Node
{
    [Export]
    public PlayerController Player;
    public PackedScene SkateboardModel;
    public Vector3 SkateboardStandingPoint = Vector3.Zero;
    private Node3D SpawnedSkateboardModel;

    [Export]
    public Quaternion SkateboardRotationOffset = Quaternion.Identity;
    public bool CanBoost = false;
    public bool FloatOnWater = false;

    [Export]
    public float FloatVelocity = 5f;
    public bool IsSkating { private set; get; }

    [Export]
    public string[] TrickAnims = new string[0];

    private bool OriginallyCouldManuallyRoll;
    private bool OriginallyCouldSpinDash;
    private bool OriginallyCouldDropDash;
    private bool OriginallyCouldRollDuringJump;
    private float OriginalAntiInertiaFactor;
    private ProcessModeEnum OriginalBoostProcessMode;
    public bool IsTricking = false;
    private float TrickingTime = 0;

    [Export]
    public string TrickUp;

    [Export]
    public string TrickDown;

    [Export]
    public string TrickLeft;

    [Export]
    public string TrickRight;

    public int TrickCount;

    [Export]
    public int MaxTrickCount = 5;

    [Export]
    public float PostTrickSpeedBoost = 50f;

    private Dictionary<Node, ProcessModeEnum> DisabledActionModes = new Dictionary<Node, ProcessModeEnum>();

    [Export]
    public AudioStreamPlayer3D AnnouncerAudioPlayer;

    [Export]
    public AudioStreamPlayer3D TrickAudioPlayer;

    [Export]
    public AudioStream[] ComboAnnouncerVoices = new AudioStream[0];

    [Export]
    public AudioStream TrickSuccessSound;

    [Export]
    public AudioStream TrickFailSound;

    [Export]
    public GpuParticles3D TrickParticle;

    [Export]
    public GpuParticles3D TrickLandParticle;

    [Export]
    public Color[] TrickColors =
    {
        new(1, 0, 0, 1),
        new(1, 0, 1, 1),
        new(0, 0, 1, 1),
        new(0, 1, 0, 1),
        new(1, 1, 1, 1),
    };

    [Signal]
    public delegate void OnTrickEventHandler(int TrickCount);

    [Signal]
    public delegate void OnLandingEventHandler(bool TrickSuccess, int TrickCount);

    public override void _Ready()
    {
        Player.PlayerInventory.OnItemCollected += this.OnItemCollected;
        Player.PlayerInput.OnLeftInputFlick += this.OnLeftInputFlick;
        TrickCount = 0;
    }

    public override void _ExitTree()
    {
        if (Player != null && Player.PlayerInventory != null)
        {
            Player.PlayerInventory.OnItemCollected -= this.OnItemCollected;
        }
        if (Player != null && Player.PlayerInput != null)
        {
            Player.PlayerInput.OnLeftInputFlick -= this.OnLeftInputFlick;
        }
    }

    private void OnLeftInputFlick(Vector2 flickInput)
    {
        if (!this.IsSkating || IsTricking || Player.Grounded)
            return;

        if (Mathf.Abs(flickInput.X) < 0.5f && flickInput.Y > 0.5f)
        {
            Player.PlayerSkinController.TravelAnimation(TrickUp, true);
        }
        else if (Mathf.Abs(flickInput.X) < 0.5f && flickInput.Y < -0.5f)
        {
            Player.PlayerSkinController.TravelAnimation(TrickDown, true);
        }
        else if (flickInput.X < -0.5f && Mathf.Abs(flickInput.Y) < 0.5f)
        {
            Player.PlayerSkinController.TravelAnimation(TrickLeft, true);
        }
        else if (flickInput.X > 0.5f && Mathf.Abs(flickInput.Y) < 0.5f)
        {
            Player.PlayerSkinController.TravelAnimation(TrickRight, true);
        }
        else
        {
            return;
        }

        IsTricking = true;
        TrickingTime = 0;
        if (ComboAnnouncerVoices.Length > TrickCount)
        {
            AnnouncerAudioPlayer.Stream = ComboAnnouncerVoices[TrickCount];
            AnnouncerAudioPlayer.Play(0);
        }
        TrickAudioPlayer.Stream = TrickSuccessSound;
        TrickAudioPlayer.Play(0);
        if (TrickColors.Length > 0)
        {
            TrickParticle.ProcessMaterial.Set(
                "color",
                TrickColors[Mathf.Min(TrickCount, TrickColors.Length - 1)]
            );
        }
        TrickParticle.Restart();
        TrickParticle.Emitting = true;
        TrickCount = Mathf.Min(TrickCount + 1, MaxTrickCount);

        EmitSignal(SignalName.OnTrick, this, TrickCount);
    }

    private void OnItemCollected(string itemType, int newCount, int difference)
    {
        if (itemType == "skateboard" && newCount <= 0)
        {
            if (this.IsSkating)
            {
                StopSkating();
            }
        }
    }

    public void StartSkating()
    {
        if (IsSkating)
            return;

        var actionSuper = Player.GetNodeOrNull<ActionSuperTransformation>(
            "./PlayerControl/Actions/ActionSuperTransformation"
        );
        if (actionSuper != null && actionSuper.IsSuper)
        {
            actionSuper.StopSuper(); // Less animations hassle. xD
        }
        this.IsSkating = true;
        Player.SkateMode = true;
        Player.AllowGoingBackwards = true;
        Player.OnlyFastRotation = true;
        Player.DisableNoInputDecceleration = true;
        var actionRoll = Player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        if (actionRoll != null)
        {
            OriginallyCouldManuallyRoll = actionRoll.CanManuallyRoll;
            OriginallyCouldSpinDash = actionRoll.CanSpinDash;
            OriginallyCouldDropDash = actionRoll.CanDropDash;

            actionRoll.CanManuallyRoll = false;
            actionRoll.CanSpinDash = false;
            actionRoll.CanDropDash = false;
        }

        var actionJump = Player.GetNodeOrNull<ActionJump>("./PlayerControl/Actions/ActionJump");
        if (actionJump != null)
        {
            OriginallyCouldRollDuringJump = actionJump.RollDuringJump;

            actionJump.RollDuringJump = false;
        }

        var actionBoost = Player.GetNodeOrNull<ActionBoost>("./PlayerControl/Actions/ActionBoost");
        if (actionBoost != null)
        {
            OriginalBoostProcessMode = actionBoost.ProcessMode;
            actionBoost.ProcessMode = CanBoost ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;

            if (!CanBoost && actionBoost.IsBoosting)
            {
                actionBoost.StopBoost();
            }
        }

        OriginalAntiInertiaFactor = Player.AntiInertiaFactor;
        Player.AntiInertiaFactor = 1;
        Player.PlayerSkinController.TravelAnimation("Skate");

        SpawnSkateboardModel();
        Player.PlayerSkinController.Position = SkateboardStandingPoint;

        var actionHoming = Player.GetNodeOrNull<ActionHoming>(
            "./PlayerControl/Actions/ActionHoming"
        ); // Will be disabled later with a blanket Actions disable
        if (actionHoming != null)
        {
            actionHoming.MainTarget = null;
            OnePlayerUI.Instances[Player.PlayerID].HomingIcon.Visible = false;
        }

        var actionsParent = Player.GetNodeOrNull<Node>("./PlayerControl/Actions");
        if (actionsParent != null)
        {
            foreach (var action in actionsParent.GetChildren())
            {
                var type = action.GetType();
                if (
                    type != this.GetType()
                    && type != typeof(ActionJump)
                    && type != typeof(ActionBoost)
                    && type != typeof(ActionGrind)
                    && action.ProcessMode != ProcessModeEnum.Disabled
                )
                {
                    DisabledActionModes[action] = action.ProcessMode;
                    action.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
    }

    public void StopSkating()
    {
        this.IsSkating = false;
        Player.SkateMode = false;
        Player.AllowGoingBackwards = false;
        Player.OnlyFastRotation = false;
        Player.DisableNoInputDecceleration = false;
        var actionRoll = Player.GetNodeOrNull<ActionRoll>("./PlayerControl/Actions/ActionRoll");
        if (actionRoll != null)
        {
            actionRoll.CanManuallyRoll = OriginallyCouldManuallyRoll;
            actionRoll.CanSpinDash = OriginallyCouldSpinDash;
            actionRoll.CanDropDash = OriginallyCouldDropDash;
        }

        var actionJump = Player.GetNodeOrNull<ActionJump>("./PlayerControl/Actions/ActionJump");
        if (actionJump != null)
        {
            actionJump.RollDuringJump = OriginallyCouldRollDuringJump;
        }

        var actionBoost = Player.GetNodeOrNull<ActionBoost>("./PlayerControl/Actions/ActionBoost");
        if (actionBoost != null)
        {
            actionBoost.ProcessMode = OriginalBoostProcessMode;
        }

        Player.AntiInertiaFactor = OriginalAntiInertiaFactor;
        Player.SlopeGravityFactor = 0.3f;

        Player.PlayerSkinController.TravelAnimation("Running");
        DespawnSkateboardModel();
        Player.PlayerSkinController.Position = Vector3.Zero;

        foreach (var actionMode in DisabledActionModes)
        {
            if (GodotObject.IsInstanceValid(actionMode.Key))
            {
                actionMode.Key.ProcessMode = actionMode.Value;
            }
        }
        DisabledActionModes.Clear();

        // Disconnect signals
        foreach (
            Godot.Collections.Dictionary connection in this.GetSignalConnectionList(
                SignalName.OnTrick
            )
        )
        {
            var callable = (Callable)connection["callable"];
            if (this.IsConnected(SignalName.OnTrick, callable))
            {
                this.Disconnect(SignalName.OnTrick, callable);
            }
        }
        foreach (
            Godot.Collections.Dictionary connection in this.GetSignalConnectionList(
                SignalName.OnLanding
            )
        )
        {
            var callable = (Callable)connection["callable"];
            if (this.IsConnected(SignalName.OnLanding, callable))
            {
                this.Disconnect(SignalName.OnLanding, callable);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!this.IsSkating)
            return;
        Player.DisableNoInputDecceleration = true;

        if ((Player.LinearVelocity.Normalized()).Dot(Player.Gravity.Normalized()) >= 0)
        {
            Player.SlopeGravityFactor = 2f;
        }
        else
        {
            Player.SlopeGravityFactor = 0.1f;
        }

        if (FloatOnWater)
        {
            var waterInteraction = Player.GetNodeOrNull<WaterInteraction>(
                "./PlayerControl/WaterInteraction"
            );
            if (waterInteraction != null && waterInteraction.WaterFeetCounter > 0)
            {
                Player.LinearVelocity +=
                    -Player.Gravity.Normalized()
                    * Player.Gravity.Length()
                    * FloatVelocity
                    * (float)delta;
                Player.GroundNormal = -Player.Gravity.Normalized();
            }
        }

        if (Player.Grounded && !IsTricking && TrickCount > 0)
        {
            // Trick success
            TrickLandParticle.Emitting = true;
            Player.LinearVelocity +=
                Player.VelocityXZ.Normalized() * PostTrickSpeedBoost * TrickCount;
            EmitSignal(SignalName.OnLanding, this, TrickCount, true);
        }

        if (Player.Grounded)
        {
            TrickCount = 0;
        }

        if (IsTricking)
        {
            if (Player.Grounded)
            {
                // Trick fail
                IsTricking = false;
                Player.PlayerSkinController.TravelAnimation("Skate Ground", true);
                EmitSignal(SignalName.OnLanding, this, TrickCount, false);

                TrickAudioPlayer.Stream = TrickFailSound;
                TrickAudioPlayer.Play(0);
            }
            TrickingTime += (float)delta;
            AnimationNodeStateMachinePlayback stateMachine = (AnimationNodeStateMachinePlayback)
                Player.PlayerSkinController.AnimationTree.Get("parameters/playback");

            if (TrickingTime > 0.2f && !stateMachine.GetCurrentNode().ToString().Contains("Trick"))
            {
                IsTricking = false;
            }
        }
    }

    void SpawnSkateboardModel()
    {
        DespawnSkateboardModel();
        SpawnedSkateboardModel = SkateboardModel.Instantiate<Node3D>();
        Player.PlayerSkinController.BoneAttachmentSkateboard.AddChild(SpawnedSkateboardModel);
        SpawnedSkateboardModel.GlobalBasis = new Basis(
            (
                SkateboardRotationOffset.Normalized()
                * SpawnedSkateboardModel.GlobalBasis.GetRotationQuaternion().Normalized()
            ).Normalized()
        );
    }

    void DespawnSkateboardModel()
    {
        var skateboardModel = SpawnedSkateboardModel;
        SpawnedSkateboardModel = null;

        if (skateboardModel != null && GodotObject.IsInstanceValid(skateboardModel))
        {
            if (skateboardModel.GetParent() != null)
            {
                skateboardModel.GetParent().RemoveChild(skateboardModel);
            }

            skateboardModel.QueueFree();
        }

        foreach (var child in Player.PlayerSkinController.BoneAttachmentSkateboard.GetChildren())
        {
            Player.PlayerSkinController.BoneAttachmentSkateboard.RemoveChild(child);
            child.QueueFree();
        }
    }
}
