using System;
using Godot;
using Godot.Collections;

public partial class ButtonPromptTextLabel : Node
{
    public PlayerController Player;

    [Export(PropertyHint.MultilineText)]
    public string Text = "";

    [Export]
    public ColorRect ColorRect;

    [Export]
    public RichTextLabel RichTextLabel;

    [Export]
    public OnePlayerUI PlayerUI;

    [Export]
    public Dictionary<string, string> ControllerButtonToImageMapping =
        new Dictionary<string, string>();

    public override void _Ready()
    {
        //RichTextLabel.Visible = false;
        if (PlayerUI != null)
        {
            Player = PlayerController.Instances[PlayerUI.PlayerID];
            Player.PlayerInput.OnInputTypeChange += HandleInputTag;
        }
    }

    public override void _ExitTree()
    {
        if (Player != null)
        {
            Player.PlayerInput.OnInputTypeChange -= HandleInputTag;
        }
    }

    public void OnBodyEnter(Node3D body)
    {
        var player = body.GetNodeOrNull<PlayerController>(".");
        if (player != null && !player.NpcPartnerControl.IsNpc)
        {
            this.Player = player;
            RichTextLabel.Visible = true;

            HandleInputTag();
            if (ColorRect != null)
            {
                ColorRect.Size = RichTextLabel.Size;
                ColorRect.GlobalPosition = RichTextLabel.GlobalPosition;
                ColorRect.SetAnchorsPreset(RichTextLabel.LayoutPreset.FullRect, true);
            }
        }
    }

    public void OnBodyExit(Node3D body)
    {
        var player = body.GetNodeOrNull<PlayerController>(".");
        if (player == this.Player)
        {
            this.Player = null;
            RichTextLabel.Visible = false;
        }
    }

    public void HandleInputTag()
    {
        string startTag = "[input]";
        string endTag = "[/input]";
        string newText = this.Text;
        RichTextLabel.Text = "";
        if (this.Text.Length == 0)
            return;
        while (true)
        {
            var startTagPos = newText.FindN(startTag);
            var endTagPos = newText.FindN(endTag);
            if (startTagPos == -1 && endTagPos == -1)
            {
                break;
            }
            if (startTagPos != -1 && endTagPos == -1)
            {
                throw new ArgumentException("Unterminated [input] tag");
            }
            if (startTagPos == -1 && endTagPos != -1)
            {
                throw new ArgumentException("Found closing [/input] tag without opening [input]");
            }
            var preText = newText.Substring(0, startTagPos);
            var insideText = newText.Substring(
                startTagPos + startTag.Length,
                endTagPos - (startTagPos + startTag.Length)
            );
            var postText = newText.Substring(endTagPos + endTag.Length);

            string inputText = GetInputText(insideText);

            newText = preText + inputText + postText;
        }
        RichTextLabel.Text = newText;
    }

    string GetInputText(string action)
    {
        if (Player == null || Player.PlayerInput == null)
            return action;

        switch (Player.PlayerInput.LastInputType)
        {
            case PlayerInput.InputType.KEYBOARD_AND_MOUSE:
                return GetKeyboardAndMouseInputText(action);
            case PlayerInput.InputType.PAD:
                return GetJoypadInputText(action);
            case PlayerInput.InputType.TOUCHSCREEN:
                return GetTouchInputText(action);
            default:
                return action;
        }
    }

    string GetKeyboardAndMouseInputText(string action)
    {
        if (OS.GetName() == "Android" || OS.GetName() == "iOS")
            return GetTouchInputText(action);

        var keyboardAction = "kb_" + action;
        var keyboardText = "";
        if (
            OptionsMenu.Options.KeyboardInput.TryGetValue(keyboardAction, out var key)
            && key != Key.None
        )
            keyboardText = OS.GetKeycodeString(key);

        var mouseText = "";
        if (
            OptionsMenu.Options.MouseButtonInput.TryGetValue(action, out var mouseButton)
            && mouseButton >= 0
        )
            mouseText = GetMouseButtonText((MouseButton)mouseButton);

        if (keyboardText.Length > 0 && mouseText.Length > 0)
            return keyboardText + " / " + mouseText;
        if (keyboardText.Length > 0)
            return keyboardText;
        if (mouseText.Length > 0)
            return mouseText;

        return "Unassigned";
    }

    string GetTouchInputText(string action)
    {
        if (ControllerButtonToImageMapping.ContainsKey("touch_" + action))
            return "[img]" + ControllerButtonToImageMapping["touch_" + action] + "[/img]";

        if (action.Contains("_actioncharaswap_"))
            return "";

        return action;
    }

    string GetMouseButtonText(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "Mouse Left",
            MouseButton.Right => "Mouse Right",
            MouseButton.Middle => "Mouse Middle",
            MouseButton.WheelUp => "Mouse Wheel Up",
            MouseButton.WheelDown => "Mouse Wheel Down",
            MouseButton.WheelLeft => "Mouse Wheel Left",
            MouseButton.WheelRight => "Mouse Wheel Right",
            _ => button.ToString(),
        };
    }

    string GetJoypadInputText(string action)
    {
        string padText = "";

        var identifier = Player.PlayerInput.PlayerIdentifier;
        if (
            (identifier == "any" || identifier.StartsWith("pad"))
            && Input.GetConnectedJoypads().Count > 0
        )
        {
            var events = InputMap.ActionGetEvents(identifier + "_" + action);
            InputEvent ev = null;
            foreach (var inputEvent in events)
            {
                if (inputEvent is InputEventJoypadButton || inputEvent is InputEventJoypadMotion)
                {
                    ev = inputEvent;
                    break;
                }
            }

            if (ev != null)
            {
                int padNum = 0;
                if (identifier.StartsWith("pad"))
                {
                    var padNumStr = identifier.Substring(3).Split("_")[0];
                    padNum = Int32.Parse(padNumStr);
                }

                var joyName = Input.GetJoyName(padNum).ToLower();
                if (joyName.Length == 0)
                {
                    return action; // How did this happen? Joypad without name? This value is a fallback.
                }

                var eventText = ev.AsText();
                var eventTextSplit = eventText.Split(
                    new string[] { "(", ")", "," },
                    StringSplitOptions.TrimEntries
                );
                // Axes and some controller names do not include the button-name fields.
                if (eventTextSplit.Length < 5)
                    return GetJoypadEventFallback(ev);

                var numberName = eventTextSplit[0];
                var genericName = eventTextSplit[1];
                var playstationName = eventTextSplit[2];
                var xboxName = eventTextSplit[3];
                var nintendoName = eventTextSplit[4];

                // Please refer to https://github.com/mdqinc/SDL_GameControllerDB/blob/master/gamecontrollerdb.txt for controller names.
                if (joyName.Contains("xbox") || joyName.Contains("xinput"))
                {
                    var xboxText = xboxName.Split(" ")[1]; // First part contains "Xbox"
                    if (xboxName.Split(" ")[0].ToLower() == "d-pad")
                    {
                        xboxText = xboxName;
                    }

                    if (ControllerButtonToImageMapping.ContainsKey("Xbox_" + xboxText.ToUpper()))
                    {
                        padText =
                            "[img]"
                            + ControllerButtonToImageMapping["Xbox_" + xboxText.ToUpper()]
                            + "[/img]";
                    }
                    else
                    {
                        switch (xboxText.ToUpper())
                        {
                            case "A":
                                padText = "[color=green]A[/color]";
                                break;
                            case "B":
                                padText = "[color=red]B[/color]";
                                break;
                            case "X":
                                padText = "[color=blue]X[/color]";
                                break;
                            case "Y":
                                padText = "[color=yellow]Y[/color]";
                                break;
                            case "D-PAD LEFT":
                                padText = "🡄";
                                break;
                            case "D-PAD RIGHT":
                                padText = "🡆";
                                break;
                            case "D-PAD UP":
                                padText = "🡅";
                                break;
                            case "D-PAD DOWN":
                                padText = "🡇";
                                break;
                            default:
                                padText = xboxText;
                                break;
                        }
                    }
                }
                else if (
                    joyName.Contains("sony")
                    || joyName.Contains("dualshock")
                    || joyName.Contains("playstation")
                    || joyName.Contains("ps")
                )
                {
                    var psName = playstationName.Split(" ")[1]; // First part contains "Sony";
                    if (playstationName.Split(" ")[0].ToLower() == "d-pad")
                    {
                        psName = playstationName;
                    }
                    if (ControllerButtonToImageMapping.ContainsKey("PS_" + psName.ToUpper()))
                    {
                        padText =
                            "[img]"
                            + ControllerButtonToImageMapping["PS_" + psName.ToUpper()]
                            + "[/img]";
                    }
                    else
                    {
                        switch (psName.ToLower())
                        {
                            case "cross":
                                padText = "[color=blue]×[/color]";
                                break;
                            case "triangle":
                                padText = "[color=green]△[/color]";
                                break;
                            case "circle":
                                padText = "[color=red]○[/color]";
                                break;
                            case "square":
                                padText = "[color=pink]□[/color]";
                                break;
                            case "D-PAD LEFT":
                                padText = "🡄";
                                break;
                            case "D-PAD RIGHT":
                                padText = "🡆";
                                break;
                            case "D-PAD UP":
                                padText = "🡅";
                                break;
                            case "D-PAD DOWN":
                                padText = "🡇";
                                break;
                            default:
                                padText = psName;
                                break;
                        }
                    }
                }
                else if (
                    joyName.Contains("nintendo")
                    || joyName.Contains("wii")
                    || joyName.Contains("gamecube")
                    || joyName.Contains("switch")
                )
                {
                    padText = nintendoName.Split(" ")[1]; // First part contains "Nintendo";
                }
                else
                {
                    padText =
                        genericName.Split(" ")[0]
                        + " Button ("
                        + numberName.Split(" ", StringSplitOptions.TrimEntries)[2]
                        + ")"; // Second part contains "Action"
                }
            }
        }
        return padText.Length > 0 ? padText : "Unassigned";
    }

    string GetJoypadEventFallback(InputEvent inputEvent)
    {
        if (inputEvent is InputEventJoypadMotion motion)
            return GetJoypadAxisName(motion.Axis);

        if (inputEvent is InputEventJoypadButton button)
            return GetJoypadButtonName(button.ButtonIndex);

        return "Unassigned";
    }

    string GetJoypadAxisName(JoyAxis axis)
    {
        return axis switch
        {
            JoyAxis.LeftX => "Left Stick X",
            JoyAxis.LeftY => "Left Stick Y",
            JoyAxis.RightX => "Right Stick X",
            JoyAxis.RightY => "Right Stick Y",
            JoyAxis.TriggerLeft => "Left Trigger",
            JoyAxis.TriggerRight => "Right Trigger",
            _ => axis.ToString(),
        };
    }

    string GetJoypadButtonName(JoyButton button)
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
            JoyButton.DpadUp => "↑",
            JoyButton.DpadDown => "↓",
            JoyButton.DpadLeft => "←",
            JoyButton.DpadRight => "→ ",
            _ => button.ToString(),
        };
    }
}
