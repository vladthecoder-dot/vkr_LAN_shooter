using Godot;
using Godot.Collections;
using System.Linq;

public partial class GameOverScreen : Control
{
    public const string ScenePath = "res://Interface/Scenes/GameOverScreen.tscn";

    [Export] public Button DisconnectButton { get; private set; }
    [Export] public Button RestartButton { get; private set; }
    [Export] public RichTextLabel ScoreTable { get; private set; }
    [Export] public Label GameOverLabel { get; private set; }

    [Export] public string WinnerText { get; private set; } = "Win";
    [Export] public string LoserText { get; private set; } = "Lose";

    [Export] public AudioStreamPlayer WinSound { get; private set; }
    [Export] public AudioStreamPlayer LoseSound { get; private set; }

    public Dictionary<long, int> Scoreboard;
    public long WinnerId = 1;

    public override void _Ready()
    {
        if (!Multiplayer.IsServer()) RestartButton.Disabled = true;

        if(Multiplayer.GetUniqueId() == WinnerId)
        {
            GameOverLabel.Text = WinnerText;
            WinSound.Play();
        }
        else
        {
            GameOverLabel.Text = LoserText;
            LoseSound.Play();
        }

        GD.Print(Scoreboard);
        var sortedScore = Scoreboard.OrderBy(x => x.Value);
        ScoreTable.Text = string.Empty;
        foreach (var row in Scoreboard)
        {            
            ScoreTable.Text += $"{NetworkManager.PeerNames[row.Key]} - {row.Value}\n";
        }

        DisconnectButton.Pressed += async() =>
        {
            await NetworkManager.Instance.DisconnectFromGame();
            QueueFree();
        };

        RestartButton.Pressed += async() =>
        {
            await NetworkManager.Instance.RestartMap();
            QueueFree();
        };

        NetworkManager.Instance.MapLoaded += QueueFree;
    }

    public override void _ExitTree()
    {
        NetworkManager.Instance.MapLoaded -= QueueFree;
    }

}
