using Godot;
using System;
using System.Xml.Linq;

public partial class WaitRoom : Control
{
    public const string ScenePath = "res://Interface/Scenes/WaitRoom.tscn";
    [Export] public Label WaitRoomLabel { get; private set; }
    [Export] public Label PlayerCountLabel { get; private set; }
    [Export] public Button DisconnectButton { get; private set; }
    [Export] public Button StartButton { get; private set; }
    [Export] public VBoxContainer PlayerList { get; private set; }

    public override void _Ready()
    {
        PlayerCountLabel.Text = $"{Multiplayer.GetPeers().Length + 1}/{NetworkManager.MaximumPlayers}";
        if (Multiplayer.IsServer())
        {
            WaitRoomLabel.Text = "Waiting for the players to connect";
        }
        else
        {
            WaitRoomLabel.Text = "Waiting for host to start game";
        }
        Multiplayer.PeerConnected += Connected;
        Multiplayer.PeerDisconnected += Disconnected;
        Multiplayer.ServerDisconnected += QueueFree;

        if (Multiplayer.GetPeers().Length + 1 >= 2 && Multiplayer.IsServer()) StartButton.Disabled = false;
        StartButton.Pressed += () => NetworkManager.Instance.EmitSignal(NetworkManager.SignalName.GameStarted);

        NetworkManager.Instance.GameStarted += QueueFree;
        NetworkManager.Instance.PeerListUpdated += UpdatePlayerList;

        DisconnectButton.Pressed += async() =>
        {
            await NetworkManager.Instance.DisconnectFromGame();
            QueueFree();
        };

        UpdatePlayerList();
        MoveToFront();
    }

    private void UpdatePlayerList()
    {
        foreach (var oldButton in PlayerList.GetChildren()) oldButton.QueueFree();
        
        foreach(var playerName in NetworkManager.PeerNames)
        {
            var button = new Button();
            button.Text = playerName.Value + ((playerName.Key == Multiplayer.GetUniqueId()) ? " - you" : string.Empty);
            button.AddThemeFontSizeOverride("font_size", 20);

            button.SetMeta("Peer", playerName.Key);
            button.Pressed += async() =>
            { 
                int id = button.GetMeta("Peer").As<int>();
                if (id == Multiplayer.GetUniqueId()) return;
                await NetworkManager.Instance.KickPlayer(id);
            };
            PlayerList.AddChild(button);
        }
    }

    private void Connected(long id)
    {
        GD.Print($"Wait screen - peer connected: {id}");
        int count = Multiplayer.GetPeers().Length + 1;
        PlayerCountLabel.Text = $"{count}/{NetworkManager.MaximumPlayers}";
        if (Multiplayer.IsServer() && count >= 2)
        {
            StartButton.Disabled = false;
            WaitRoomLabel.Text = "Press play to start";
        }
    }

    private void Disconnected(long id)
    {
        GD.Print($"Wait screen - peer disconnected: {id}");
        int count = Multiplayer.GetPeers().Length + 1;
        PlayerCountLabel.Text = $"{count}/{NetworkManager.MaximumPlayers}";
        if (Multiplayer.IsServer() && count < 2)
        {
            StartButton.Disabled = true;
            WaitRoomLabel.Text = "Waiting for the players to connect";
        }
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= Connected;
        Multiplayer.PeerDisconnected -= Disconnected;
        Multiplayer.ServerDisconnected -= QueueFree;
        NetworkManager.Instance.GameStarted -= QueueFree;
        NetworkManager.Instance.PeerListUpdated -= UpdatePlayerList;
    }
}
