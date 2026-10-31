using Godot;
using System;
using System.Linq;

public partial class UserOptions : Node
{
    public const string SettingsPath = "user://settings.cfg";

    public const string AttackUI = "ui_fire", ThrowUI = "ui_alt_fire", MeleeUI = "ui_punch", BackflipUI = "ui_backflip", SlideUI = "ui_slide", JumpUI = "ui_jump";
    public static readonly string[] UserInputs = [AttackUI, ThrowUI, MeleeUI, BackflipUI, SlideUI, JumpUI];

    public static readonly Vector2I Windowed = new(1280, 720);
    public static readonly Vector2I Fullscreen = new(1920, 1080);

    public static UserOptions Instance { get; private set; }

    [Signal] public delegate void SettingsLoadedEventHandler();

    public override void _Ready()
    {
        Instance = this;

        LoadControls();
        LoadWindow();
        LoadVolume();

        EmitSignal(SignalName.SettingsLoaded);
        GD.Print("Settings loaded");
    }

    public static ConfigFile GetSettings()
    {
        var config = new ConfigFile();
        config.Load(SettingsPath);
        return config;
    }

    public static void SaveVolume()
    {
        var config = GetSettings();
        config.SetValue("Sound", "volume", AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("Master")));
        config.SetValue("Sound", "muted", AudioServer.IsBusMute(AudioServer.GetBusIndex("Master")));
        config.Save(SettingsPath);
    }

    public static void LoadVolume()
    {
        var config = GetSettings();
        if (config.HasSection("Sound"))
        {
            AudioServer.SetBusVolumeLinear(AudioServer.GetBusIndex("Master"), config.GetValue("Sound", "volume").As<float>());
            AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), config.GetValue("Sound", "muted").As<bool>());
        }
        else
        {
            AudioServer.SetBusVolumeLinear(AudioServer.GetBusIndex("Master"), 50);
        }
    }

    public static void SaveName()
    {
        if (NetworkManager.PlayerName == string.Empty) return;
        var config = GetSettings();
        config.SetValue("Player", "name", NetworkManager.PlayerName);
        config.Save(SettingsPath);
    }

    public static string LoadName()
    {
        var config = GetSettings();
        if (!config.HasSection("Player")) return "nameless";
        return config.GetValue("Player", "name").As<string>();
    }

    public static void SaveWindow()
    {
        var config = GetSettings();
        config.SetValue("Window", "fullscreen", DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen);
        config.Save(SettingsPath);
    }

    public static void LoadWindow()
    {
        var config = GetSettings();
        if (!config.HasSection("Window")) return;

        bool fullscreen = config.GetValue("Window", "fullscreen").As<bool>();

        DisplayServer.WindowSetMode(fullscreen?DisplayServer.WindowMode.Fullscreen: DisplayServer.WindowMode.Windowed);

        DisplayServer.WindowSetSize(fullscreen ? Fullscreen : Windowed);
    }

    public static void SaveControls()
    {
        var config = GetSettings();

        foreach (string action in InputMap.GetActions())
        {
            if (!UserInputs.Contains(action)) continue;

            var events = InputMap.ActionGetEvents(action);

            if (events.Count > 0) config.SetValue("Controls", action, events[0]);
        }

        config.Save(SettingsPath);
    }

    public static void LoadControls()
    {
        var config = GetSettings();
        if (!config.HasSection("Controls")) return;

        foreach (string action in config.GetSectionKeys("Controls"))
        {
            InputEvent savedEvent = config.GetValue("Controls", action).As<InputEvent>();

            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, savedEvent);
        }
    }

    public static string GetActionKeyName(string action)
    {
        InputEvent[] events = [.. InputMap.ActionGetEvents(action)];
        return events[0].AsText();
    }
}
