using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

public partial class OptionsMenu : Control
{
    const string SaveFileName = "options.json";

    static bool IsCompatibilityRenderer =>
        ProjectSettings.GetSetting("rendering/renderer/rendering_method").AsString() == "gl_compatibility";

    static bool IsMobilePlatform => OS.GetName() == "Android" || OS.GetName() == "iOS";

    [Export] public Control DefaultFocus;
    [Export] public Slider AudioSliderMaster;
    [Export] public Slider AudioSliderMusic;
    [Export] public Slider AudioSliderEffects;
    [Export] public OptionButton WindowedOption;
    [Export] public OptionButton WindowSizeOption;
    [Export] public Slider ResolutionScaleSlider;
    [Export] public Label ResolutionScaleLabel;
    [Export] public OptionButton ScalingModeOption;
    [Export] public OptionButton GraphicsPresetOption;
    [Export] public OptionButton VsyncOption;
    [Export] public OptionButton AaTaaOption;
    [Export] public OptionButton AaMsaaOption;
    [Export] public OptionButton AaFxaaOption;
    [Export] public OptionButton ShadowResolutionOption;
    [Export] public OptionButton ShadowFilteringOption;
    [Export] public OptionButton ModelQualityOption;
    [Export] public OptionButton GIOption;
    [Export] public OptionButton BloomOption;
    [Export] public OptionButton AOOption;
    [Export] public OptionButton SSROption;
    [Export] public OptionButton SSLOption;
    [Export] public OptionButton VolumetricFogOption;

    [Export] public Slider RenderDistanceSlider;
    [Export] public Label RenderDistanceLabel;
    [Export] public CheckButton ShowFpsButton;
    [Export] public OptionButton MaxFpsOption;

    [Export] public OptionButton CameraDynamicFovOption;
    [Export] public OptionButton CameraSmoothingOption;
    [Export] public OptionButton CameraAutoAirMoveOption;
    [Export] public OptionButton CameraVomitCameraOption;
    [Export] public OptionButton CameraAutoLookDownOption;

    [Export] public GridContainer KeyboardButtonsContainer;
    [Export] public Label KeyboardLabelTemplate;
    [Export] public Button KeyboardButtonTemplate;

    [Export] public Slider MouseXSensitivitySlider;
    [Export] public Label MouseXSensitivityValue;
    [Export] public Slider MouseYSensitivitySlider;
    [Export] public Label MouseYSensitivityValue;

    private Button[] InputButtons = new Button[0];
    private string[] InputActions = new string[0];
    private readonly Dictionary<string, Button> JoypadButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Button> MouseButtons = new Dictionary<string, Button>();

    private enum RecordingInputKind
    {
        Keyboard,
        JoypadButton,
        JoypadAxis,
        MouseButton
    }

    private RecordingInputKind RecordingInput;
    private bool RecordingInputArmed;

    [Export]
    public Godot.Collections.Dictionary<string, string> KbActionsToDescriptions = new Godot.Collections.Dictionary<string, string>()
    {
        { "kb_actionjump", "Jump" },
        { "kb_actionroll", "Roll/Drift" },
        { "kb_actiondash", "Dash/Boost" },
        { "kb_actionhoming", "Homing attack" },
        { "kb_actionstomp", "Stomp" },
        { "kb_actionsuper", "Super transformation" },
        { "kb_actioncharaswap_l", "Change character ←" },
        { "kb_actioncharaswap_r", "Change character →" },
        { "kb_moveup", "Move ↑" },
        { "kb_movedown", "Move ↓" },
        { "kb_moveleft", "Move ←" },
        { "kb_moveright", "Move →" },
        { "kb_camup", "Camera ↑" },
        { "kb_camdown", "Camera ↓" },
        { "kb_camleft", "Camera ←" },
        { "kb_camright", "Camera →" },
        { "kb_quickstep_l", "Quick step ←" },
        { "kb_quickstep_r", "Quick step →" },
    };

    public Vector2I[] Resolutions = new Vector2I[0];

    public class OptionsFile
    {
        [JsonIgnore] public string GameVersion;
        [JsonInclude] public float VolumeMaster = 0.5f;
        [JsonInclude] public float VolumeMusic = 0.5f;
        [JsonInclude] public float VolumeSFX = 0.5f;
        [JsonInclude] public int Windowed = 0;
        [JsonInclude] public float ResolutionScale = 1f;
        [JsonInclude] public int WindowSizeX = 1920;
        [JsonInclude] public int WindowSizeY = 1080;
        [JsonInclude] public int GraphicsPreset = 0;
        [JsonInclude] public int VSync = 1;
        [JsonInclude] public int ScalingMode = 0;
        [JsonInclude] public int AaTAA = 0;
        [JsonInclude] public int AaMSAA = 0;
        [JsonInclude] public int AaFXAA = 0;
        [JsonInclude] public int ShadowResolution = 0;
        [JsonInclude] public int ShadowFiltering = 0;
        [JsonInclude] public int ModelQuality = 0;
        [JsonInclude] public int GI = 0;
        [JsonInclude] public int Bloom = 0;
        [JsonInclude] public int AO = 0;
        [JsonInclude] public int SSR = 0;
        [JsonInclude] public int SSL = 0;
        [JsonInclude] public int VolumetricFog = 0;
        [JsonInclude] public float RenderDistance = 4000f;
        [JsonInclude] public bool ShowFps = false;
        [JsonInclude] public int MaxFps = 0;

        [JsonInclude] public int CameraDynamicFov = 1;
        [JsonInclude] public int CameraSmoothing = 0;
        [JsonInclude] public int CameraAutoAirMove = 0;
        [JsonInclude] public int CameraVomitCamera = 1;
        [JsonInclude] public int CameraAutoLookDown = 1;

        [JsonInclude] public float MouseSensitivityX = 0.1f;
        [JsonInclude] public float MouseSensitivityY = 0.1f;

        [JsonInclude] public Dictionary<string, Key> KeyboardInput = new Dictionary<string, Key>();
        [JsonInclude] public Dictionary<string, int> JoypadButtonInput = new Dictionary<string, int>();
        [JsonInclude] public Dictionary<string, int> JoypadAxisInput = new Dictionary<string, int>();
        [JsonInclude] public Dictionary<string, int> JoypadAxisDirectionInput = new Dictionary<string, int>();
        [JsonInclude] public Dictionary<string, int> MouseButtonInput = new Dictionary<string, int>();

        // Default input mappings are kept together so each device's controls are easy to audit.
        public static readonly Dictionary<string, Key> DefaultKeyboardInput = new Dictionary<string, Key>()
        {
            { "kb_actionjump", Key.Space },
            { "kb_actionroll", Key.Ctrl },
            { "kb_actiondash", Key.Shift },
            { "kb_actionhoming", Key.J },
            { "kb_actionstomp", Key.K },
            { "kb_actionsuper", Key.Backspace },
            { "kb_actioncharaswap_l", Key.Key1 },
            { "kb_actioncharaswap_r", Key.Key3 },
            { "kb_moveup", Key.W },
            { "kb_movedown", Key.S },
            { "kb_moveleft", Key.A },
            { "kb_moveright", Key.D },
            { "kb_camup", Key.Kp8 },
            { "kb_camdown", Key.Kp2 },
            { "kb_camleft", Key.Kp4 },
            { "kb_camright", Key.Kp6 },
            { "kb_quickstep_l", Key.Q },
            { "kb_quickstep_r", Key.E },
        };

        public static readonly Dictionary<string, JoyButton> DefaultJoypadButtons = new Dictionary<string, JoyButton>()
        {
            { "actionjump", JoyButton.A },
            { "actionhoming", JoyButton.X },
            { "actionsuper", JoyButton.Back },
            { "actionstomp", JoyButton.B },
            { "quickstep_l", JoyButton.LeftShoulder },
            { "quickstep_r", JoyButton.RightShoulder },
            { "actioncharaswap_l", JoyButton.DpadLeft },
            { "actioncharaswap_r", JoyButton.DpadRight },
        };

        public static readonly Dictionary<string, JoyAxis> DefaultJoypadAxes = new Dictionary<string, JoyAxis>()
        {
            { "actionroll", JoyAxis.TriggerLeft },
            { "actiondash", JoyAxis.TriggerRight },
            { "moveleft", JoyAxis.LeftX },
            { "moveright", JoyAxis.LeftX },
            { "moveup", JoyAxis.LeftY },
            { "movedown", JoyAxis.LeftY },
            { "camleft", JoyAxis.RightX },
            { "camright", JoyAxis.RightX },
            { "camup", JoyAxis.RightY },
            { "camdown", JoyAxis.RightY },
        };

        public static readonly Dictionary<string, int> DefaultJoypadAxisDirections = new Dictionary<string, int>()
        {
            { "actionroll", 1 },
            { "actiondash", 1 },
            { "moveleft", -1 },
            { "moveright", 1 },
            { "moveup", -1 },
            { "movedown", 1 },
            { "camleft", -1 },
            { "camright", 1 },
            { "camup", -1 },
            { "camdown", 1 },
        };

        public static readonly Dictionary<string, int> DefaultMouseButtons = new Dictionary<string, int>()
        {
            { "actionjump", -1 },
            { "actionroll", (int)MouseButton.Right },
            { "actiondash", -1 },
            { "actionhoming", (int)MouseButton.Left },
            { "actionstomp", (int)MouseButton.Middle },
            { "actionsuper", -1 },
            { "actioncharaswap_l", (int)MouseButton.WheelDown },
            { "actioncharaswap_r", (int)MouseButton.WheelUp },
            { "moveup", -1 },
            { "movedown", -1 },
            { "moveleft", -1 },
            { "moveright", -1 },
            { "camup", -1 },
            { "camdown", -1 },
            { "camleft", -1 },
            { "camright", -1 },
            { "quickstep_l", -1 },
            { "quickstep_r", -1 },
        };

        public void Save()
        {
            this.GameVersion = ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString();

            var serializedSave = JsonSerializer.Serialize(this);
            using var saveFile = FileAccess.Open("user://" + SaveFileName, FileAccess.ModeFlags.Write);
            saveFile.StoreString(serializedSave);
        }

        public static OptionsFile Load()
        {
            OptionsFile loadedSave;
            if (!FileAccess.FileExists("user://" + SaveFileName))
            {
                loadedSave = new OptionsFile(); // No save data
            }
            else
            {
                using var saveFile = FileAccess.Open("user://" + SaveFileName, FileAccess.ModeFlags.Read);
                var serializedSave = saveFile.GetAsText();

                loadedSave = JsonSerializer.Deserialize<OptionsFile>(serializedSave);

            }
            // Apply defaults to unassigned buttons
            foreach (var kbAction in OptionsFile.DefaultKeyboardInput.Keys)
            {
                if (!loadedSave.KeyboardInput.ContainsKey(kbAction))
                {
                    loadedSave.KeyboardInput.Add(kbAction, DefaultKeyboardInput[kbAction]);
                }
            }
            foreach (var action in OptionsFile.DefaultJoypadButtons.Keys)
                if (!loadedSave.JoypadButtonInput.ContainsKey(action)) loadedSave.JoypadButtonInput[action] = (int)OptionsFile.DefaultJoypadButtons[action];
            foreach (var action in OptionsFile.DefaultJoypadAxes.Keys)
            {
                if (!loadedSave.JoypadAxisInput.ContainsKey(action)) loadedSave.JoypadAxisInput[action] = (int)OptionsFile.DefaultJoypadAxes[action];
                if (!loadedSave.JoypadAxisDirectionInput.ContainsKey(action)) loadedSave.JoypadAxisDirectionInput[action] = OptionsFile.DefaultJoypadAxisDirections[action];
            }
            foreach (var action in OptionsFile.DefaultMouseButtons.Keys)
                if (!loadedSave.MouseButtonInput.ContainsKey(action)) loadedSave.MouseButtonInput[action] = OptionsFile.DefaultMouseButtons[action];
            return loadedSave;
        }


    }
    public static OptionsFile Options;
    private bool RecordingKeyboardInput;
    private string RecordingKeyboardInputAction;
    private string RecordingAction;

    public void OnWindowedOptionChange(int selection)
    {
        OptionsMenu.Options.Windowed = selection;
        GetResolutions();

    }

    public void OnWindowSizeOptionChange(int selection)
    {
        OptionsMenu.Options.WindowSizeX = Resolutions[selection].X;
        OptionsMenu.Options.WindowSizeY = Resolutions[selection].Y;
    }

    public void OnResolutionScaleChange(float scale)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.ResolutionScale = scale;
        UpdateLabels();
    }

    public void OnGraphicsPresetChange(int selection)
    {
        OptionsMenu.Options.GraphicsPreset = selection;
        switch (selection)
        {
            case 1:
                SetGraphicsPreset(0.5f, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 2000f);
                break;
            case 2:
                SetGraphicsPreset(0.75f, 0, 1, 1, 2, 2, 1, 1, 1, 2, 1, 1, 0, 4000f);
                break;
            case 3:
                SetGraphicsPreset(1f, 1, 1, 2, 4, 4, 2, 2, 2, 4, 3, 4, 1, 8000f);
                break;
        }

        UpdateGraphicsControls();
        GetResolutions();
        UpdateLabels();
    }

    void SetGraphicsPreset(float resolutionScale, int scalingMode, int taa, int msaa, int shadowResolution, int shadowFiltering, int modelQuality, int gi, int bloom, int ao, int ssr, int ssl, int volumetricFog, float renderDistance)
    {
        OptionsMenu.Options.ResolutionScale = resolutionScale;
        OptionsMenu.Options.ScalingMode = scalingMode;
        OptionsMenu.Options.AaTAA = taa;
        OptionsMenu.Options.AaMSAA = msaa;
        OptionsMenu.Options.AaFXAA = 0;
        OptionsMenu.Options.ShadowResolution = shadowResolution;
        OptionsMenu.Options.ShadowFiltering = shadowFiltering;
        OptionsMenu.Options.ModelQuality = modelQuality;
        OptionsMenu.Options.GI = gi;
        OptionsMenu.Options.Bloom = bloom;
        OptionsMenu.Options.AO = ao;
        OptionsMenu.Options.SSR = ssr;
        OptionsMenu.Options.SSL = ssl;
        OptionsMenu.Options.VolumetricFog = volumetricFog;
        OptionsMenu.Options.RenderDistance = renderDistance;
    }

    void MarkGraphicsCustom()
    {
        OptionsMenu.Options.GraphicsPreset = 0;
        if (GraphicsPresetOption != null)
            GraphicsPresetOption.Selected = 0;
    }

    public void OnVsyncChange(int selection)
    {
        OptionsMenu.Options.VSync = selection;
    }

    public void OnAaTAAChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.AaTAA = selection;
    }

    public void OnAaMSAAChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.AaMSAA = selection;
    }
    public void OnAaFXAAChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.AaFXAA = selection;
    }

    public void OnScalingModeChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.ScalingMode = selection;
    }

    public void OnShadowResolutionChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.ShadowResolution = selection;
    }

    public void OnShadowFilteringChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.ShadowFiltering = selection;
    }

    public void OnModelQualityChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.ModelQuality = selection;
    }

    public void OnGIChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.GI = selection;
    }

    public void OnBloomChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.Bloom = selection;
    }

    public void OnAOChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.AO = selection;
    }

    public void OnSSRChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.SSR = selection;
    }

    public void OnSSLChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.SSL = selection;
    }
    public void OnVolumetricFogChange(int selection)
    {
        MarkGraphicsCustom();
        OptionsMenu.Options.VolumetricFog = selection;
    }

    public void OnRestoreDefaultKeyboardControls()
    {
        OptionsMenu.Options.KeyboardInput.Clear();
        foreach (var kbAction in OptionsFile.DefaultKeyboardInput.Keys)
        {
            OptionsMenu.Options.KeyboardInput.Add(kbAction, OptionsFile.DefaultKeyboardInput[kbAction]);
        }
        UpdateLabels();
    }

    public void OnRestoreDefaultJoypadControls()
    {
        OptionsMenu.Options.JoypadButtonInput.Clear();
        foreach (var action in OptionsFile.DefaultJoypadButtons)
            OptionsMenu.Options.JoypadButtonInput[action.Key] = (int)action.Value;
        OptionsMenu.Options.JoypadAxisInput.Clear();
        OptionsMenu.Options.JoypadAxisDirectionInput.Clear();
        foreach (var action in OptionsFile.DefaultJoypadAxes)
        {
            OptionsMenu.Options.JoypadAxisInput[action.Key] = (int)action.Value;
            OptionsMenu.Options.JoypadAxisDirectionInput[action.Key] = OptionsFile.DefaultJoypadAxisDirections[action.Key];
        }
        UpdateLabels();
    }

    public void OnRestoreDefaultMouseControls()
    {
        OptionsMenu.Options.MouseButtonInput.Clear();
        foreach (var action in OptionsFile.DefaultMouseButtons)
            OptionsMenu.Options.MouseButtonInput[action.Key] = action.Value;
        UpdateLabels();
    }

    public void OnRestoreMouseOptions()
    {
        OptionsMenu.Options.MouseSensitivityX = new OptionsFile().MouseSensitivityX;
        OptionsMenu.Options.MouseSensitivityY = new OptionsFile().MouseSensitivityY;
        OnRestoreDefaultMouseControls();
        UpdateLabels();
    }

    public void Apply()
    {
        bool compatibility = IsCompatibilityRenderer;
        if (compatibility)
        {
            OptionsMenu.Options.AaFXAA = 0;
            OptionsMenu.Options.GI = 0;
            OptionsMenu.Options.AO = 0;
            OptionsMenu.Options.SSR = 0;
            OptionsMenu.Options.SSL = 0;
            OptionsMenu.Options.VolumetricFog = 0;
        }

        // Windowed/Fullscreen is not configurable on mobile platforms.
        if (!IsMobilePlatform)
        {
            switch (OptionsMenu.Options.Windowed)
            {
                case 0:
                    GetTree().Root.Mode = Window.ModeEnum.Windowed;
                    break;

                case 1:
                    GetTree().Root.Mode = Window.ModeEnum.Fullscreen;
                    break;

                case 2:
                    GetTree().Root.Mode = Window.ModeEnum.ExclusiveFullscreen;
                    break;
            }

            if (OptionsMenu.Options.Windowed == 0)
            {
                DisplayServer.WindowSetSize(new Vector2I(OptionsMenu.Options.WindowSizeX, OptionsMenu.Options.WindowSizeY));
            }
            else
            {
                DisplayServer.WindowSetSize(DisplayServer.ScreenGetUsableRect().Size);
            }
        }
        DisplayServer.WindowSetVsyncMode((DisplayServer.VSyncMode)OptionsMenu.Options.VSync);
        Engine.MaxFps = OptionsMenu.Options.MaxFps;
        // Resolution scale
        ProjectSettings.SetSetting("rendering/scaling_3d/scale", OptionsMenu.Options.ResolutionScale);
        // Antialiasing
        ProjectSettings.SetSetting("rendering/anti_aliasing/quality/use_taa", OptionsMenu.Options.AaTAA == 1);
        ProjectSettings.SetSetting("rendering/anti_aliasing/quality/msaa_3d", OptionsMenu.Options.AaMSAA);
        if (!compatibility)
            ProjectSettings.SetSetting("rendering/anti_aliasing/quality/screen_space_aa", OptionsMenu.Options.AaFXAA);
        // Effects
        if (!compatibility)
        {
            switch (OptionsMenu.Options.GI)
            {
                case 0:
                    ProjectSettings.SetSetting("rendering/global_illumination/gi/use_half_resolution", true);
                    ProjectSettings.SetSetting("rendering/global_illumination/voxel_gi/quality", 0);
                    break;
                case 1:
                    ProjectSettings.SetSetting("rendering/global_illumination/gi/use_half_resolution", false);
                    ProjectSettings.SetSetting("rendering/global_illumination/voxel_gi/quality", 0);
                    break;
                case 2:
                    ProjectSettings.SetSetting("rendering/global_illumination/gi/use_half_resolution", false);
                    ProjectSettings.SetSetting("rendering/global_illumination/voxel_gi/quality", 1);
                    break;
            }

            ProjectSettings.SetSetting("rendering/environment/ssao/quality", OptionsMenu.Options.AO);
            ProjectSettings.SetSetting("rendering/environment/ssil/quality", OptionsMenu.Options.SSL);
            ProjectSettings.SetSetting("rendering/environment/screen_space_reflection/roughness_quality", OptionsMenu.Options.SSR);
            ProjectSettings.SetSetting("rendering/environment/volumetric_fog/use_filter", OptionsMenu.Options.VolumetricFog);
        }
        //ProjectSettings.SetSetting("", OptionsMenu.Options.Bloom);	// TODO: Bloom is attached to environment
        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.Far = OptionsMenu.Options.RenderDistance;
        }

        // Keyboard input
        foreach (var keyboardInput in OptionsMenu.Options.KeyboardInput)
        {
            RemapKeyboard(keyboardInput.Key, keyboardInput.Value);
        }
        foreach (var binding in OptionsMenu.Options.JoypadButtonInput)
            RemapJoypadButton(binding.Key, (JoyButton)binding.Value);
        foreach (var binding in OptionsMenu.Options.JoypadAxisInput)
            RemapJoypadAxis(binding.Key, (JoyAxis)binding.Value, OptionsMenu.Options.JoypadAxisDirectionInput[binding.Key]);
        foreach (var binding in OptionsMenu.Options.MouseButtonInput)
            RemapMouseButton(binding.Key, (MouseButton)binding.Value);

        // Volume
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), Mathf.LinearToDb(OptionsMenu.Options.VolumeMaster));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Music"), Mathf.LinearToDb(OptionsMenu.Options.VolumeMusic));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("SFX"), Mathf.LinearToDb(OptionsMenu.Options.VolumeSFX));



        UpdatePlayerCameraSettings();
    }

    public void OnBack()
    {
        OptionsMenu.Options = OptionsFile.Load();
        // This menu can be in both pause and main menu. We need to react in both cases.
        if (PauseMenu.Instance != null)
        {
            PauseMenu.Instance.Pause();
        }
        else if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.ToMainMenu();
        }

        SetDefaultScrolling();
        RecordingKeyboardInput = false;
        RecordingInput = RecordingInputKind.Keyboard;
        RecordingInputArmed = false;

    }

    public void OnApply()
    {
        Apply();
        OptionsMenu.Options.Save();
        OnBack();
    }

    public override void _Ready()
    {
        //try
        //{
        OptionsMenu.Options = OptionsFile.Load();
        PopulateKeyboardButtons();
        PopulateJoypadButtons();
        PopulateMouseButtons();
        UpdateLabels();
        UpdateGraphicsControls();

        CameraDynamicFovOption.Selected = OptionsMenu.Options.CameraDynamicFov;
        CameraSmoothingOption.Selected = OptionsMenu.Options.CameraSmoothing;
        CameraAutoAirMoveOption.Selected = OptionsMenu.Options.CameraAutoAirMove;
        CameraVomitCameraOption.Selected = OptionsMenu.Options.CameraVomitCamera;
        CameraAutoLookDownOption.Selected = OptionsMenu.Options.CameraAutoLookDown;

        UpdateLabels();
        GetResolutions();

        if (IsCompatibilityRenderer)
        {
			OptionsMenu.Options.AaFXAA = 0;
            OptionsMenu.Options.GI = 0;
            OptionsMenu.Options.AO = 0;
            OptionsMenu.Options.SSR = 0;
            OptionsMenu.Options.SSL = 0;
            OptionsMenu.Options.VolumetricFog = 0;

            foreach (var option in new[] { AaFxaaOption, GIOption, AOOption, SSROption, SSLOption, VolumetricFogOption })
            {
                option.Visible = false;
                var parent = option.GetParent();
                int optionIndex = option.GetIndex();
                (parent.GetChild(optionIndex - 1) as CanvasItem).Visible = false;
                (parent.GetChild(optionIndex + 1) as CanvasItem).Visible = false;
            }
        }

        var platform = OS.GetName();
        if (IsMobilePlatform)
        {
            WindowedOption.Disabled = true;
            WindowSizeOption.Disabled = true;
        }

        UpdatePlayerCameraSettings();
        SetDefaultScrolling();
        Apply();
        //} catch (Exception e)
        //{
        //	ErrorLog.AddMessage(e.Message + e.StackTrace.ToString());
        //}

    }

    void UpdateLabels()
    {
        var resolution = GetTree().Root.Size;
        resolution.X = (int)(resolution.X * OptionsMenu.Options.ResolutionScale);
        resolution.Y = (int)(resolution.Y * OptionsMenu.Options.ResolutionScale);
        ResolutionScaleLabel.Text = OptionsMenu.Options.ResolutionScale.ToString("0.00") + "x (" + resolution.X + "x" + resolution.Y + ")";

        for (int i = 0; i < InputButtons.Length; i++)
        {
            string kbText = "Unassigned";
            if (OptionsMenu.Options.KeyboardInput.ContainsKey(InputActions[i]))
            {
                var key = OptionsMenu.Options.KeyboardInput[InputActions[i]];
                kbText = key == Key.None ? "Unassigned" : OS.GetKeycodeString(key);
            }
            InputButtons[i].Text = kbText;
        }
        foreach (var binding in JoypadButtons)
        {
            if (OptionsMenu.Options.JoypadButtonInput.ContainsKey(binding.Key) && OptionsMenu.Options.JoypadButtonInput[binding.Key] >= 0)
                binding.Value.Text = GetJoyButtonName((JoyButton)OptionsMenu.Options.JoypadButtonInput[binding.Key]);
            else if (OptionsMenu.Options.JoypadAxisInput.ContainsKey(binding.Key))
                binding.Value.Text = OptionsMenu.Options.JoypadAxisInput[binding.Key] >= 0 ? "Axis " + OptionsMenu.Options.JoypadAxisInput[binding.Key] : "Unassigned";
            else
                binding.Value.Text = "Unassigned";
        }
        foreach (var binding in MouseButtons)
            binding.Value.Text = OptionsMenu.Options.MouseButtonInput.ContainsKey(binding.Key) && OptionsMenu.Options.MouseButtonInput[binding.Key] >= 0 ? GetMouseButtonName((MouseButton)OptionsMenu.Options.MouseButtonInput[binding.Key]) : "Unassigned";

        AudioSliderMaster.Value = OptionsMenu.Options.VolumeMaster;
        AudioSliderEffects.Value = OptionsMenu.Options.VolumeSFX;
        AudioSliderMusic.Value = OptionsMenu.Options.VolumeMusic;

        RenderDistanceLabel.Text = (OptionsMenu.Options.RenderDistance / 1000f).ToString() + " km";


        MouseXSensitivityValue.Text = (OptionsMenu.Options.MouseSensitivityX * 100).ToString() + "%";
        MouseXSensitivitySlider.Value = OptionsMenu.Options.MouseSensitivityX;
        MouseYSensitivityValue.Text = (OptionsMenu.Options.MouseSensitivityY * 100).ToString() + "%";
        MouseYSensitivitySlider.Value = OptionsMenu.Options.MouseSensitivityY;
    }

    void UpdateGraphicsControls()
    {
        WindowedOption.Selected = OptionsMenu.Options.Windowed;
        GraphicsPresetOption.Selected = OptionsMenu.Options.GraphicsPreset;
        VsyncOption.Selected = OptionsMenu.Options.VSync;
        ResolutionScaleSlider.Value = OptionsMenu.Options.ResolutionScale;
        ScalingModeOption.Selected = OptionsMenu.Options.ScalingMode;
        AaTaaOption.Selected = OptionsMenu.Options.AaTAA;
        AaMsaaOption.Selected = OptionsMenu.Options.AaMSAA;
        AaFxaaOption.Selected = OptionsMenu.Options.AaFXAA;
        ShadowResolutionOption.Selected = OptionsMenu.Options.ShadowResolution;
        ShadowFilteringOption.Selected = OptionsMenu.Options.ShadowFiltering;
        ModelQualityOption.Selected = OptionsMenu.Options.ModelQuality;
        GIOption.Selected = OptionsMenu.Options.GI;
        BloomOption.Selected = OptionsMenu.Options.Bloom;
        AOOption.Selected = OptionsMenu.Options.AO;
        SSROption.Selected = OptionsMenu.Options.SSR;
        SSLOption.Selected = OptionsMenu.Options.SSL;
        VolumetricFogOption.Selected = OptionsMenu.Options.VolumetricFog;
        RenderDistanceSlider.Value = OptionsMenu.Options.RenderDistance;
        ShowFpsButton.ButtonPressed = OptionsMenu.Options.ShowFps;
        MaxFpsOption.Selected = MaxFpsOption.GetItemIndex(OptionsMenu.Options.MaxFps);
    }

    public void SetDefaultFocus()
    {
        DefaultFocus.GrabFocus();
    }

    public void OnRemapKeyboardButton(string actionName)
    {
        RecordingInput = RecordingInputKind.Keyboard;
        RecordingKeyboardInput = true;
        RecordingKeyboardInputAction = actionName;
        RecordingInputArmed = false;
        ArmRecordingInputNextFrame();

        for (int i = 0; i < InputButtons.Length; i++)
        {
            if (InputActions[i] == actionName)
            {
                InputButtons[i].Text = "Press a keyboard button";
                break;
            }
        }
    }

    void BeginRemap(string actionName, RecordingInputKind kind, Button button)
    {
        RecordingAction = actionName;
        RecordingInput = kind;
        RecordingInputArmed = false;
        ArmRecordingInputNextFrame();
        button.Text = kind == RecordingInputKind.JoypadAxis ? "Move an axis" : "Press a button";
    }

    async void ArmRecordingInputNextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (Visible) RecordingInputArmed = true;
    }

    public override void _Input(InputEvent @event)
    {
        try
        {
            if (!this.Visible) return;

            if (RecordingInputArmed && RecordingInput == RecordingInputKind.Keyboard && @event is InputEventKey inputEventKey)
            {
                UpdateLabels();
                if (@event.IsActionPressed("ui_cancel")) return;
                if (!inputEventKey.IsPressed()) return;

                var keycode = inputEventKey.Keycode;
                GetViewport().SetInputAsHandled();
                RemapKeyboard(RecordingKeyboardInputAction, keycode);
                OptionsMenu.Options.KeyboardInput[RecordingKeyboardInputAction] = keycode;

                RecordingKeyboardInput = false;
                RecordingInputArmed = false;
                UpdateLabels();
            }
            else if (RecordingInputArmed && RecordingInput == RecordingInputKind.JoypadButton && @event is InputEventJoypadButton joypadButton && joypadButton.Pressed)
            {
                GetViewport().SetInputAsHandled();
                OptionsMenu.Options.JoypadButtonInput[RecordingAction] = (int)joypadButton.ButtonIndex;
                RemapJoypadButton(RecordingAction, joypadButton.ButtonIndex);
                RecordingInput = RecordingInputKind.Keyboard;
                RecordingInputArmed = false;
                UpdateLabels();
            }
            else if (RecordingInputArmed && RecordingInput == RecordingInputKind.JoypadAxis && @event is InputEventJoypadMotion joypadMotion && Mathf.Abs(joypadMotion.AxisValue) > 0.5f)
            {
                GetViewport().SetInputAsHandled();
                OptionsMenu.Options.JoypadAxisInput[RecordingAction] = (int)joypadMotion.Axis;
                OptionsMenu.Options.JoypadAxisDirectionInput[RecordingAction] = joypadMotion.AxisValue < 0 ? -1 : 1;
                RemapJoypadAxis(RecordingAction, joypadMotion.Axis, OptionsMenu.Options.JoypadAxisDirectionInput[RecordingAction]);
                RecordingInput = RecordingInputKind.Keyboard;
                RecordingInputArmed = false;
                UpdateLabels();
            }
            else if (RecordingInputArmed && RecordingInput == RecordingInputKind.MouseButton && @event is InputEventMouseButton mouseButton && mouseButton.Pressed)
            {
                GetViewport().SetInputAsHandled();
                OptionsMenu.Options.MouseButtonInput[RecordingAction] = (int)mouseButton.ButtonIndex;
                RemapMouseButton(RecordingAction, mouseButton.ButtonIndex);
                RecordingInput = RecordingInputKind.Keyboard;
                RecordingInputArmed = false;
                UpdateLabels();
            }
            else if (@event.IsActionPressed("ui_cancel"))
            {
                OnBack();
            }
        }
        catch (Exception e)
        {
            ErrorLog.AddMessage(e.Message + e.StackTrace.ToString());

        }
    }

    void RemapKeyboard(string actionName, Key keycode)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        InputMap.ActionEraseEvents(actionName);
        InputEventKey newEvent = new InputEventKey();
        newEvent.Keycode = keycode;
        if (keycode != Key.None) InputMap.ActionAddEvent(actionName, newEvent);


        if (actionName.Substring(0, 3) == "kb_")
        {
            var anyAction = "any_" + actionName.Substring(3);
            if (!InputMap.HasAction(anyAction))
            {
                InputMap.AddAction(anyAction);
            }
            else
            {
                foreach (var action in InputMap.ActionGetEvents(anyAction))
                {
                    if (action is InputEventKey)
                    {
                        InputMap.ActionEraseEvent(anyAction, action);
                    }
                }
            }

            if (keycode != Key.None) InputMap.ActionAddEvent(anyAction, newEvent);

        }
    }

    void RemapJoypadButton(string actionName, JoyButton button)
    {
        for (int device = 0; device < 4; device++)
        {
            var action = "pad" + device + "_" + actionName;
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            InputMap.ActionEraseEvents(action);
            if ((int)button >= 0)
                InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button, Device = device });
        }
        ReplaceAnyJoypadEvent(actionName, (int)button >= 0 ? new InputEventJoypadButton { ButtonIndex = button } : null);
    }

    void RemapJoypadAxis(string actionName, JoyAxis axis, int direction)
    {
        for (int device = 0; device < 4; device++)
        {
            var action = "pad" + device + "_" + actionName;
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            InputMap.ActionEraseEvents(action);
            if ((int)axis >= 0)
                InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = direction, Device = device });
        }
        ReplaceAnyJoypadEvent(actionName, (int)axis >= 0 ? new InputEventJoypadMotion { Axis = axis, AxisValue = direction } : null);
    }

    void ReplaceAnyJoypadEvent(string actionName, InputEvent input)
    {
        var action = "any_" + actionName;
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var oldInput in InputMap.ActionGetEvents(action).ToArray())
            if (oldInput is InputEventJoypadButton || oldInput is InputEventJoypadMotion) InputMap.ActionEraseEvent(action, oldInput);
        if (input != null) InputMap.ActionAddEvent(action, input);
    }

    void RemapMouseButton(string actionName, MouseButton button)
    {
        foreach (var prefix in new[] { "kb_", "any_" })
        {
            var action = prefix + actionName;
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            foreach (var oldInput in InputMap.ActionGetEvents(action).ToArray())
                if (oldInput is InputEventMouseButton) InputMap.ActionEraseEvent(action, oldInput);
            if ((int)button >= 0) InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
        }
    }

    void UnassignRecording()
    {
        switch (RecordingInput)
        {
            case RecordingInputKind.Keyboard:
                OptionsMenu.Options.KeyboardInput[RecordingKeyboardInputAction] = Key.None;
                RemapKeyboard(RecordingKeyboardInputAction, Key.None);
                break;
            case RecordingInputKind.JoypadButton:
                OptionsMenu.Options.JoypadButtonInput[RecordingAction] = -1;
                RemapJoypadButton(RecordingAction, (JoyButton)(-1));
                break;
            case RecordingInputKind.JoypadAxis:
                OptionsMenu.Options.JoypadAxisInput[RecordingAction] = -1;
                RemapJoypadAxis(RecordingAction, (JoyAxis)(-1), 1);
                break;
            case RecordingInputKind.MouseButton:
                OptionsMenu.Options.MouseButtonInput[RecordingAction] = -1;
                RemapMouseButton(RecordingAction, (MouseButton)(-1));
                break;
        }
        RecordingInputArmed = false;
        RecordingKeyboardInput = false;
        RecordingInput = RecordingInputKind.Keyboard;
        UpdateLabels();
    }

    void UnassignAction(RecordingInputKind kind, string actionName)
    {
        RecordingInput = kind;
        RecordingAction = actionName;
        RecordingKeyboardInputAction = actionName.StartsWith("kb_") ? actionName : "kb_" + actionName;
        UnassignRecording();
    }

    public void OnAudioMasterSliderChange(float value)
    {
        OptionsMenu.Options.VolumeMaster = value;
    }

    public void OnAudioMusicSliderChange(float value)
    {
        OptionsMenu.Options.VolumeMusic = value;
    }

    public void OnAudioSFXSliderChange(float value)
    {
        OptionsMenu.Options.VolumeSFX = value;
    }

    public void OnRenderDistanceChange(float value)
    {
        // TODO: It doesn't work instantly, requires restart.
        OptionsMenu.Options.RenderDistance = value;
        UpdateLabels();
    }

    public void OnShowFpsChange(bool value)
    {
        OptionsMenu.Options.ShowFps = value;
    }

    public void OnMaxFpsChange(int selection)
    {
        OptionsMenu.Options.MaxFps = MaxFpsOption.GetItemId(selection);
    }

    public void OnCameraDynamicFovChange(int value)
    {
        OptionsMenu.Options.CameraDynamicFov = value;
    }

    public void OnCameraSmoothingChange(int value)
    {
        OptionsMenu.Options.CameraSmoothing = value;
    }

    public void OnCameraAutoAirChange(int value)
    {
        OptionsMenu.Options.CameraAutoAirMove = value;
    }

    public void OnCameraVomitCameraChange(int value)
    {
        OptionsMenu.Options.CameraVomitCamera = value;
    }

    public void OnCameraAutoLookDown(int value)
    {
        OptionsMenu.Options.CameraAutoLookDown = value;
    }

    public void GetResolutions()
    {
        if (IsMobilePlatform)
        {
            SetWindowSettingsVisible(false);
            return;
        }

        SetWindowSettingsVisible(true);
        HashSet<Vector2I> resolutionsSet = new HashSet<Vector2I>();
        if (OptionsMenu.Options.Windowed != 0)
        {
            WindowSizeOption.Disabled = true;
            resolutionsSet.Add(DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen(0)));

            ScalingModeOption.Disabled = false;
        }
        else
        {
            WindowSizeOption.Disabled = false;
            resolutionsSet.Add(new Vector2I(OptionsMenu.Options.WindowSizeX, OptionsMenu.Options.WindowSizeY));
            resolutionsSet.Add(DisplayServer.WindowGetSize());
            for (int s = 0; s < DisplayServer.GetScreenCount(); s++)
            {
                var baseResolution = DisplayServer.ScreenGetSize(s);
                for (int i = 1; i <= 4; i++)
                {
                    Vector2I resolution = baseResolution;
                    resolution.X /= i;
                    resolution.Y /= i;
                    resolutionsSet.Add(resolution);
                }
            }

            ScalingModeOption.Disabled = true;
            ScalingModeOption.Selected = 0;
        }

        WindowSizeOption.Clear();
        Resolutions = resolutionsSet.ToArray();
        foreach (var resolution in Resolutions)
        {
            WindowSizeOption.AddItem(resolution.X + " x " + resolution.Y);
        }

        WindowSizeOption.Select(0);
    }

    void SetWindowSettingsVisible(bool visible)
    {
        WindowedOption.GetParent().GetNode<Control>("Label5").Visible = visible;
        WindowedOption.GetParent().GetNode<Control>("Filler").Visible = visible;
        WindowedOption.Visible = visible;
        WindowSizeOption.GetParent().GetNode<Control>("Label7").Visible = visible;
        WindowSizeOption.GetParent().GetNode<Control>("Filler4").Visible = visible;
        WindowSizeOption.Visible = visible;
        WindowedOption.Disabled = !visible;
        WindowSizeOption.Disabled = !visible;
    }

    public static void UpdatePlayerCameraSettings()
    {
        foreach (var cam in PlayerCamera.Instances)
        {
            cam.Value.DynamicFov = OptionsMenu.Options.CameraDynamicFov == 1;
            cam.Value.SettingEasing = OptionsMenu.Options.CameraSmoothing == 1;
            cam.Value.VomitCamera = OptionsMenu.Options.CameraVomitCamera == 1;
            cam.Value.AutoLookDown = OptionsMenu.Options.CameraAutoLookDown == 1;
            if (StageData.Instance.Style == StageData.StageStyle.SpecialStage)
            {
                cam.Value.SettingAutoAirCamera = OptionsMenu.Options.CameraAutoAirMove != 0;
            }
            else
            {
                cam.Value.SettingAutoAirCamera = OptionsMenu.Options.CameraAutoAirMove == 1;
            }
        }
    }

    void SetDefaultScrolling()
    {
        SetDefaultScrolling(this);
    }

    void SetDefaultScrolling(Node node)
    {
        if (node is TabContainer tab)
        {
            tab.CurrentTab = 0;
        }
        foreach (var childNode in node.GetChildren())
        {
            SetDefaultScrolling(childNode);

        }
    }

    void PopulateKeyboardButtons()
    {
        KeyboardButtonsContainer.LayoutDirection = Control.LayoutDirectionEnum.Ltr;
        InputActions = new string[OptionsMenu.Options.KeyboardInput.Count];
        InputButtons = new Button[OptionsMenu.Options.KeyboardInput.Count];
        for (int i = 0; i < OptionsMenu.Options.KeyboardInput.Count; i++)
        {
            var buttonKey = OptionsMenu.Options.KeyboardInput.Keys.ToArray()[i];
            if (!KbActionsToDescriptions.ContainsKey(buttonKey)) continue;

            var labelDuplicate = KeyboardLabelTemplate.Duplicate() as Label;
            labelDuplicate.Text = KbActionsToDescriptions[buttonKey];
            KeyboardButtonsContainer.AddChild(labelDuplicate);

            var buttonDuplicate = KeyboardButtonTemplate.Duplicate() as Button;
            var removeButton = new Button { Text = "Remove" };
            KeyboardButtonsContainer.AddChild(buttonDuplicate);
            KeyboardButtonsContainer.AddChild(removeButton);
            buttonDuplicate.Pressed += () => OnRemapKeyboardButton(buttonKey);
            removeButton.Pressed += () => UnassignAction(RecordingInputKind.Keyboard, buttonKey);

            InputActions[i] = buttonKey;
            InputButtons[i] = buttonDuplicate;
        }
        KeyboardLabelTemplate.Visible = false;
        KeyboardButtonTemplate.Visible = false;
        MoveControlRowToEnd(KeyboardButtonsContainer, "Default", "Filler", "Filler2");
        KeyboardLabelTemplate.QueueFree();
        KeyboardButtonTemplate.QueueFree();
        KeyboardButtonsContainer.GetNodeOrNull<Control>("TemplateFiller")?.QueueFree();
    }

    void PopulateJoypadButtons()
    {
        var container = GetNode<GridContainer>("OptionsTabs/Controls/TabContainer/Joypad");
        foreach (var child in container.GetChildren())
            child.QueueFree();
        foreach (var action in OptionsFile.DefaultJoypadButtons.Keys)
        {
            var label = new Label { Text = GetInputDescription(action) };
            var button = new Button { Text = GetJoyButtonName((JoyButton)OptionsMenu.Options.JoypadButtonInput[action]) };
            var remove = new Button { Text = "Remove" };
            container.AddChild(label);
            container.AddChild(button);
            container.AddChild(remove);
            JoypadButtons[action] = button;
            button.Pressed += () => BeginRemap(action, RecordingInputKind.JoypadButton, button);
            remove.Pressed += () => UnassignAction(RecordingInputKind.JoypadButton, action);
        }
        foreach (var action in OptionsFile.DefaultJoypadAxes.Keys)
        {
            var label = new Label { Text = GetInputDescription(action) + " axis" };
            var axis = OptionsMenu.Options.JoypadAxisInput[action];
            var button = new Button { Text = axis >= 0 ? "Axis " + axis : "Unassigned" };
            var remove = new Button { Text = "Remove" };
            container.AddChild(label);
            container.AddChild(button);
            container.AddChild(remove);
            JoypadButtons[action] = button;
            button.Pressed += () => BeginRemap(action, RecordingInputKind.JoypadAxis, button);
            remove.Pressed += () => UnassignAction(RecordingInputKind.JoypadAxis, action);
        }
        var restore = new Button { Text = "Restore defaults" };
        container.AddChild(restore);
        container.AddChild(new Control());
        container.AddChild(new Control());
        restore.Pressed += OnRestoreDefaultJoypadControls;
    }

    void PopulateMouseButtons()
    {
        var container = GetNode<GridContainer>("OptionsTabs/Controls/TabContainer/Mouse");
        foreach (var action in OptionsFile.DefaultMouseButtons.Keys)
        {
            var label = new Label { Text = GetInputDescription(action) };
            var binding = OptionsMenu.Options.MouseButtonInput[action];
            var button = new Button { Text = binding >= 0 ? GetMouseButtonName((MouseButton)binding) : "Unassigned" };
            var remove = new Button { Text = "Remove" };
            container.AddChild(label);
            container.AddChild(button);
            container.AddChild(remove);
            MouseButtons[action] = button;
            button.Pressed += () => BeginRemap(action, RecordingInputKind.MouseButton, button);
            remove.Pressed += () => UnassignAction(RecordingInputKind.MouseButton, action);
        }
        MoveControlRowToEnd(container, "Default", "Filler", "Filler2");
    }

    void MoveControlRowToEnd(GridContainer container, params string[] names)
    {
        foreach (var name in names)
        {
            var child = container.GetNodeOrNull<Node>(name);
            if (child == null) continue;
            container.RemoveChild(child);
            container.AddChild(child);
        }
    }

    string GetInputDescription(string action)
    {
        foreach (var entry in KbActionsToDescriptions)
            if (entry.Key == "kb_" + action) return entry.Value;
        return action;
    }

    string GetJoyButtonName(JoyButton button)
    {
        return button switch
        {
            JoyButton.A => "A",
            JoyButton.B => "B",
            JoyButton.X => "X",
            JoyButton.Y => "Y",
            JoyButton.Back => "Back",
            JoyButton.Start => "Start",
            JoyButton.LeftShoulder => "LB",
            JoyButton.RightShoulder => "RB",
            JoyButton.DpadLeft => "D-Pad Left",
            JoyButton.DpadRight => "D-Pad Right",
            _ => button.ToString()
        };
    }

    string GetMouseButtonName(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "Left",
            MouseButton.Right => "Right",
            MouseButton.Middle => "Middle",
            MouseButton.WheelUp => "Wheel Up",
            MouseButton.WheelDown => "Wheel Down",
            _ => button.ToString()
        };
    }
}
