using Godot;
using Godot.Collections;

public partial class LanBroadcaster : Node
{
    public const int BroadcastPort = 8999;

    private PacketPeerUdp _udpBroadcast = new();
    private Timer _broadcastTimer;

    private Dictionary _rules;

    public override void _Ready()
    {
        _rules = NetworkManager.Rules;

        _broadcastTimer = new Timer();
        _broadcastTimer.WaitTime = 1.0f;
        _broadcastTimer.Autostart = true;
        _broadcastTimer.Timeout += BroadcastServerInfo;
        AddChild(_broadcastTimer);

        _udpBroadcast.SetBroadcastEnabled(true);
        _udpBroadcast.ConnectToHost("255.255.255.255", BroadcastPort);
        GetTree().SceneChanged += async () =>
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().SceneChanged += QueueFree;
        };
        NetworkManager.Instance.GameStarted += QueueFree;

        GD.Print($"Lan broadcaster bound to port {BroadcastPort}");
    }
    
    private void BroadcastServerInfo()
    {
                            //host name                 //map name       //players count                       //maximum players    //availability for connection
        string payload = $"{NetworkManager.PlayerName}|{_rules["Name"]}|{NetworkManager.Instance.PlayersCount}|{_rules["Maximum"]}|{!NetworkManager.Peer.RefuseNewConnections}";
        byte[] packetData = System.Text.Encoding.UTF8.GetBytes(payload);

        GD.Print("Broadcasting info: " + payload);
        _udpBroadcast.PutPacket(packetData);
    }

    public override void _ExitTree()
    {
        _udpBroadcast.Close();
        _broadcastTimer.Dispose();
        NetworkManager.Instance.GameStarted -= QueueFree;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _udpBroadcast.Close();
            _broadcastTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
