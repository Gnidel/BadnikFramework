using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using static OptionsMenu;

public partial class ExtrasMenu : Control
{
    [Export]
    public Control DefaultFocus;

    [Export]
    public CheckButton BoostEnabledButton;

    [Export]
    public CheckButton SuperFlyButton;

    [Export]
    public CheckButton HeroesSwapButton;
    const string SaveFileName = "extras.json";

    public class ExtrasFile
    {
        [JsonIgnore]
        public string GameVersion;

        [JsonInclude]
        public bool BoostEnabled;

        [JsonInclude]
        public bool SuperFly;

        [JsonInclude]
        public bool HeroesSwap;

        public void Save()
        {
            this.GameVersion = ProjectSettings
                .GetSetting("application/config/version", "0.0.0")
                .AsString();

            var serializedSave = JsonSerializer.Serialize(this);
            using var saveFile = FileAccess.Open(
                "user://" + SaveFileName,
                FileAccess.ModeFlags.Write
            );
            saveFile.StoreString(serializedSave);
        }

        public static ExtrasFile Load()
        {
            ExtrasFile loadedSave;
            if (!FileAccess.FileExists("user://" + SaveFileName))
            {
                loadedSave = new ExtrasFile(); // No save data
            }
            else
            {
                using var saveFile = FileAccess.Open(
                    "user://" + SaveFileName,
                    FileAccess.ModeFlags.Read
                );
                var serializedSave = saveFile.GetAsText();

                loadedSave = JsonSerializer.Deserialize<ExtrasFile>(serializedSave);
            }
            return loadedSave;
        }
    }

    public static ExtrasFile Options;

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
    }

    public void OnApply()
    {
        Apply();
        ExtrasMenu.Options.Save();
        OnBack();
    }

    public void Apply()
    {
        Options.BoostEnabled = BoostEnabledButton.ButtonPressed;
        Options.SuperFly = SuperFlyButton.ButtonPressed;
        Options.HeroesSwap = HeroesSwapButton.ButtonPressed;
    }

    void UpdateLabels()
    {
        BoostEnabledButton.ButtonPressed = Options.BoostEnabled;
        SuperFlyButton.ButtonPressed = Options.SuperFly;
        HeroesSwapButton.ButtonPressed = Options.HeroesSwap;
    }

    public override void _Ready()
    {
        Options = ExtrasFile.Load();
        UpdateLabels();
    }

    public void SetDefaultFocus()
    {
        DefaultFocus.GrabFocus();
    }
}
