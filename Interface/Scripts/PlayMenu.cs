using Godot;

public partial class PlayMenu : Control
{
    [Export] public Button CloseButton { get; private set; }
    [Export] public VBoxContainer ServerList { get; private set; }
    [Export] public ConnectionMenu Menu { get; private set; }
    [Export] public GameCreationMenu GameCreation { get; private set; }

    public override void _Ready()
    {
        CloseButton.Pressed += Hide;
        Menu.TryingToHostGame += GameCreation.Show;
        Menu.TryingToConnectToGame += NetworkManager.Instance.JoinGame;

        if (LanListener.Instance == null)
        {
            PutLabel("LAN listener is unavailable");
            return;
        }
        else
        {
            PutLabel("No servers found");
        }

        LanListener.Instance.ListUpdated += () =>
        {
            foreach(var item in ServerList.GetChildren()) item.QueueFree();
            if (LanListener.Instance.DiscoveredServers.Count <= 0)
            {
                PutLabel("No servers found");
                return;
            }
            foreach (var item in LanListener.Instance.DiscoveredServers)
            {
                var hostButton = new Button();
                hostButton.Disabled = !bool.Parse((string)item.Value["Available"]);
                hostButton.Text = $"{item.Value["HostName"]}|{item.Value["MapName"]}|{item.Value["PlayersCount"]}/{item.Value["MaximumPlayers"]}";
                hostButton.SetMeta("Ip", item.Key);
                hostButton.Pressed += () => NetworkManager.Instance.JoinGame(hostButton.GetMeta("Ip").As<string>());
                ServerList.AddChild(hostButton);
            }
        };
    }

    private void PutLabel(string text)
    {
        var label = new Label();
        label.Text = text;
        label.VerticalAlignment = VerticalAlignment.Center;
        ServerList.AddChild(label);
    }
}
