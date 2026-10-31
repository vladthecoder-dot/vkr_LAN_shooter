using Godot;
using Godot.Collections;
using System.Net;
using System.Threading.Tasks;

// == local network manager == //
public partial class NetworkManager : Node
{
    public static ENetMultiplayerPeer Peer { get; private set; }

    public static string PlayerName = string.Empty;

    public const int Port = 7777;
    public const float NetworkTimeout = 5f;
    public const int GlobalMaximum = 6;
    public static int MaximumPlayers { get; private set; } = 6;

    public static NetworkManager Instance { get; private set; }
    public static string CurrentMapResource { get; private set; }
    public static Dictionary Rules { get; private set; }
    public static Dictionary<long, string> PeerNames { get; private set; } = [];

    public int PlayersCount { get => Multiplayer.GetPeers().Length + 1; }

    public const string PlayerKickMessage = "You have been kicked";
    public const string ServerClosedMessage = "Server closed";

    [Signal] public delegate void MapLoadedEventHandler();
    [Signal] public delegate void RulesAppliedEventHandler();
    [Signal] public delegate void GameStartedEventHandler();
    [Signal] public delegate void PeerListUpdatedEventHandler();
    [Signal] public delegate void CouldNotConnectEventHandler();

    public override void _Ready()
    {
        Instance = this;
        PlayerName = UserOptions.LoadName();

        // == network bindings == //
        Multiplayer.PeerConnected += (id) =>
        {
            RpcId(id, nameof(SetClientMap), CurrentMapResource, Rules, PeerNames);
            if(PlayersCount == MaximumPlayers)
            {
                GD.Print("Player limit reached");
                Peer.RefuseNewConnections = true;
            }
        };
        Multiplayer.PeerDisconnected += (id) =>
        {
            PeerNames.Remove(id);
            EmitSignal(SignalName.PeerListUpdated);
            if (Multiplayer.GetPeers().Length + 1 < MaximumPlayers)
            {
                Peer.RefuseNewConnections = false;
            }
        };
        Multiplayer.ServerDisconnected += async() =>
        {
            await DisconnectFromGame();

        };

        // == gameplay bindings == //
        MapLoaded += SetupRules;
        GameStarted += () =>
        {
            Peer.RefuseNewConnections = true;
        };
    }

    public async Task RestartMap()
    {
        if (!Multiplayer.IsServer()) return;        

        Rpc(nameof(DisableAllPlayers));

        // wait 2 frames for rpc signals to end
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        GetTree().ChangeSceneToFile(CurrentMapResource);
        Rpc(nameof(SetClientMap), CurrentMapResource, Rules, PeerNames);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void SetClientMap(string mapPath, Dictionary rules, Dictionary<long, string> names)
    {
        if (!ResourceLoader.Exists(mapPath)) return;
        Rules = rules;
        GetTree().ChangeSceneToFile(mapPath);
        PeerNames = names;
        Rpc(nameof(SendClientName), PlayerName);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SendClientName(string name)
    {
        //if (!Multiplayer.IsServer()) return;
        long id = Multiplayer.GetRemoteSenderId();
        if (!PeerNames.ContainsKey(id))
        {
            PeerNames.Add(id, name);
            EmitSignal(SignalName.PeerListUpdated);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void DisableAllPlayers()
    {
        foreach (var node in GetTree().GetNodesInGroup("players"))
        {
            node.SetPhysicsProcess(false);
            node.SetProcess(false);
        }
    }

    private void SetupRules()
    {
        IGameModeManager.Instance.TimeLimit = (float)Rules["Time"];
        IGameModeManager.Instance.PointsCap = (int)Rules["Points"];
        MaximumPlayers = (int)Rules["Maximum"];
        if (Multiplayer.IsServer())
        {
            var broadcaster = new LanBroadcaster();
            GetTree().Root.AddChild(broadcaster);
        }
        EmitSignal(SignalName.RulesApplied);
        GD.Print($"Rules: time - {IGameModeManager.Instance.TimeLimit}, points - {IGameModeManager.Instance.PointsCap}");
    }

    public void HostGame(string mapPath, Dictionary rules)
    {
        GD.Print("Starting host");

        Peer = new();

        var result = Peer.CreateServer(Port, GlobalMaximum);

        if(result != Error.Ok)
        {
            string msg = $"Failed to start server: {result}.\nPort is likely occupied.";
            GD.Print(msg);
            SpawnMessage(msg);
            return;
        }

        CurrentMapResource = mapPath;
        Rules = rules;
        GetTree().ChangeSceneToFile(CurrentMapResource);
        PeerNames.Clear();
        PeerNames.Add(1, PlayerName);

        Multiplayer.MultiplayerPeer = Peer;

        GD.Print("Server started at port " + Port);
    }
    

    public void JoinGame(string ip)
    {
        GD.Print("Connecting to " + ip);
        Peer = new();

        var result = Peer.CreateClient(ip, Port);        

        if (result != Error.Ok)
        {
            string msg = $"Connection failed: {result}. This IP is not hosting";
            GD.Print(msg);
            SpawnMessage(msg);
            EmitSignal(SignalName.CouldNotConnect);
            return;
        }

        Multiplayer.MultiplayerPeer = Peer;
        GD.Print("Connected");

        GetTree().CreateTimer(NetworkTimeout).Timeout += () =>
        {
            if (Peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connecting)
            {
                string msg = "Timed out";
                GD.Print(msg);
                SpawnMessage(msg);
                Peer.Close();
                EmitSignal(SignalName.CouldNotConnect);
            }
            else if(Peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected && Peer.GetUniqueId() == 1)
            {
                string msg = "Lobby is full";
                GD.Print(msg);
                SpawnMessage(msg);
                EmitSignal(SignalName.CouldNotConnect);
            }
            GD.Print($"Network status: {Peer.GetConnectionStatus()}. Id: {Peer.GetUniqueId()}");
        };

    }

    public async Task KickPlayer(int id)
    {
        if (!Multiplayer.IsServer()) return;
        RpcId(id, nameof(SpawnMessage), PlayerKickMessage);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Peer.DisconnectPeer(id);
    }

    public async Task DisconnectFromGame()
    {
        // == disabling players for less errors == //
        if (Multiplayer.IsServer())
        {
            Rpc(nameof(DisableAllPlayers));
            Rpc(nameof(SpawnMessage), ServerClosedMessage);
        }
        else
        {
            DisableAllPlayers();
        }

        // == waiting for rpc signals to end == //
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // == changing scene to main menu == //
        GetTree().ChangeSceneToFile(MainMenu.ScenePath);
        await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);

        CurrentMapResource = string.Empty;
        Rules.Clear();

        // == closing peer == //
        Peer?.Close();
        Peer = null;

        GD.Print("Disconnected");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SpawnMessage(string message)
    {
        var messageBox = GD.Load<PackedScene>(Message.ScenePath).Instantiate() as Message;
        messageBox.MessageLabel.Text = message;
        GetTree().Root.AddChild(messageBox);
    }
}

