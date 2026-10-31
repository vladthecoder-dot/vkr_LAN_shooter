using Godot;

public partial class ConnectionMenu : VBoxContainer
{
    [Export] public Button HostButton { get; private set; }
    [Export] public Button ConnectButton { get; private set; }
    [Export] public LineEdit AddressLine {  get; private set; }

    private string _connectButtonText;

    [Signal] public delegate void TryingToHostGameEventHandler();
    [Signal] public delegate void TryingToConnectToGameEventHandler(string ip);

    public override void _Ready()
    {
        _connectButtonText = ConnectButton.Text;
        HostButton.Pressed += () => EmitSignal(SignalName.TryingToHostGame);

        ConnectButton.Pressed += () =>
        {
            if (AddressLine.Text.Length == 0) return;
            ConnectButton.Text = $"Connecting to {AddressLine.Text}";
            EmitSignal(SignalName.TryingToConnectToGame, AddressLine.Text);
        };

        NetworkManager.Instance.CouldNotConnect += ResetConnectionButton;
    }

    public override void _ExitTree()
    {
        NetworkManager.Instance.CouldNotConnect -= ResetConnectionButton;
    }

    private void ResetConnectionButton() => ConnectButton.Text = _connectButtonText;
}
