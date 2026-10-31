using Godot;
using System;

public partial class OptionsMenu : Control
{
    [Export] public Button CloseButton { get; private set; }

    [Export] public CheckBox FullscreenCheckBox { get; private set; }
    [Export] public HSlider VolumeSlider { get; private set; }

    // == input change buttons == //
    [Export] public Button AttackInputChangeButton { get; private set; }
    [Export] public Button ThrowInputChangeButton { get; private set; }
    [Export] public Button MeleeInputChangeButton { get; private set; }
    [Export] public Button BackflipInputChangeButton { get; private set; }
    [Export] public Button SlideInputChangeButton { get; private set; }
    [Export] public Button JumpInputChangeButton { get; private set; }

    public string AttackInput => UserOptions.GetActionKeyName(UserOptions.AttackUI);
    public string ThrowInput => UserOptions.GetActionKeyName(UserOptions.ThrowUI);
    public string MeleeInput => UserOptions.GetActionKeyName(UserOptions.MeleeUI);
    public string BackflipInput => UserOptions.GetActionKeyName(UserOptions.BackflipUI);
    public string SlideInput => UserOptions.GetActionKeyName(UserOptions.SlideUI);
    public string JumpInput => UserOptions.GetActionKeyName(UserOptions.JumpUI);

    public bool IsListening { get; private set; } = false;

    public string CurrentInput { get; private set; } = string.Empty;

    public override void _Ready()
    {
        CloseButton.Pressed += Hide;

        // == fullscreen option init == //
        FullscreenCheckBox.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        FullscreenCheckBox.Toggled += (state) =>
        {
            DisplayServer.WindowSetMode(state ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(state ? UserOptions.Fullscreen : UserOptions.Windowed);
            UserOptions.SaveWindow();
        };

        // == volume option init == //
        VolumeSlider.Value = AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("Master"));
        VolumeSlider.ValueChanged += (value) =>
        {
            if (value <= 0)
            {
                AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), true);
                GD.Print("Sound muted");
            }
            else
            {
                AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), false);
                AudioServer.SetBusVolumeLinear(AudioServer.GetBusIndex("Master"), (float)value);
                GD.Print($"Sound changed: {value}");
            }

            UserOptions.SaveVolume();
        };

        // == input options init == //
        UpdateLabels();
        AttackInputChangeButton.Pressed += () => InputChangePressed(AttackInputChangeButton, UserOptions.AttackUI);
        ThrowInputChangeButton.Pressed += () => InputChangePressed(ThrowInputChangeButton, UserOptions.ThrowUI);
        MeleeInputChangeButton.Pressed += () => InputChangePressed(MeleeInputChangeButton, UserOptions.MeleeUI);
        BackflipInputChangeButton.Pressed += () => InputChangePressed(BackflipInputChangeButton, UserOptions.BackflipUI);
        SlideInputChangeButton.Pressed += () => InputChangePressed(SlideInputChangeButton, UserOptions.SlideUI);
        JumpInputChangeButton.Pressed += () => InputChangePressed(JumpInputChangeButton, UserOptions.JumpUI);
    }

    private void InputChangePressed(Button button, string userInput)
    {
        CurrentInput = userInput;
        button.Text = "... Press key to bind ...";
        IsListening = true;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!IsListening) return;
        if (@event is InputEventKey keyEvent && keyEvent.Keycode == Key.Escape)
        {
            IsListening = false;
            UpdateLabels();
            return;
        }
        RebindCurrent(@event);
        IsListening = false;
    }

    private void RebindCurrent(InputEvent newEvent)
    {
        InputMap.ActionEraseEvents(CurrentInput);
        InputMap.ActionAddEvent(CurrentInput, newEvent);
        CurrentInput = string.Empty;
        UserOptions.SaveControls();
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        AttackInputChangeButton.Text = AttackInput;
        ThrowInputChangeButton.Text = ThrowInput;
        MeleeInputChangeButton.Text = MeleeInput;
        BackflipInputChangeButton.Text = BackflipInput;
        SlideInputChangeButton.Text = SlideInput;
        JumpInputChangeButton.Text = JumpInput;
    }

}
