using Godot;

public partial class MainMenu : Control
{
    public static readonly string ScenePath = "res://Interface/Scenes/MainMenu.tscn";

    [Export] public Button PlayButton { get; private set; }
    [Export] public Button SettingsButton { get; private set; }
    [Export] public Button QuitButton { get; private set; }
    [Export] public PlayMenu Play { get; private set; }
    [Export] public OptionsMenu Options { get; private set; }

    [Export] public LineEdit PlayerNameEdit { get; private set; }

    public override void _Ready()
    {
        QuitButton.Pressed += () => GetTree().Quit();
        PlayButton.Pressed += Play.Show;
        SettingsButton.Pressed += Options.Show;
        PlayerNameEdit.Text = NetworkManager.PlayerName;
        PlayerNameEdit.TextChanged += (name) =>
        {
            if(name == string.Empty) return;
            NetworkManager.PlayerName = name;
            UserOptions.SaveName();
        };
    }
}
