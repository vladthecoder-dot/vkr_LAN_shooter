using Godot;
using Godot.Collections;

interface IGameModeManager
{
    public float TimeLimit { get; set; }
    public int PointsCap { get; set; }
    public Dictionary<long, int> PlayerScore { get; }
    public bool GameStarted { get; }
    public bool GameOver { get; }
    public static IGameModeManager Instance { get; protected set; }
    public Node ProjectileSpawnPoint { get; }
}

public partial class DeathmatchManager : Node2D, IGameModeManager
{
    [Export] public Node2D PlayerSpawnNode { get; private set; }
    [Export] public Label KillCounter { get; private set; }
    [Export] public Label TimerLabel { get; private set; }
    [Export] public PickupSpawner Pickups { get; private set; }
    [Export] public Node ProjectileSpawnPoint { get; private set; }
    public Dictionary<long, int> PlayerScore { get; private set; } = [];
    public SceneTreeTimer DeathmatchTimer { get; private set; }
    public Node WaitScreen { get; private set; }

    [Signal] public delegate void GameEndedEventHandler(long winnerId);

    private float _timeLimit = 0;
    public float TimeLimit 
    {
        get => _timeLimit;
        set
        {
            if(GameStarted) return;
            _timeLimit = value > 0 ? value : 0;
        }
    }

    private int _pointsCap = 0;
    public int PointsCap
    {
        get => _pointsCap;
        set
        {
            if(GameStarted) return;
            _pointsCap = value > 0 ? value : 0;
        }
    }

    public bool GameStarted { get; private set; } = false;
    public bool GameOver { get; private set; } = false;

    private void Start()
    {
        if (DeathmatchTimer == null) StartTimer();
        Pickups.TurnSpawnerOn();
        GameStarted = true;
        Rpc(nameof(WaitScreenRemove));
        GD.Print("Deathmatch");
        GD.Print($"Kill cap: {PointsCap}");
        GD.Print($"Time cap: {TimeLimit}");
        GD.Print("Game is on!");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void WaitScreenRemove() => WaitScreen.QueueFree();

    private void SpawnWaitScreen()
    {
        if (GetTree().Root.HasNode("WaitRoom")) return;
        PackedScene waitRoomScene = GD.Load<PackedScene>(WaitRoom.ScenePath);
        WaitScreen = waitRoomScene.Instantiate();
        GetTree().Root.AddChild(WaitScreen);
    }

    // == deathmatch init == //
    public override void _Ready()
    {
        NetworkManager.Instance.RulesApplied += SpawnWaitScreen;

        KillCounter.Text = string.Empty;
        TimerLabel.Text = string.Empty;

        IGameModeManager.Instance = this;
        if (!Multiplayer.IsServer())
        {
            NetworkManager.Instance.EmitSignal(NetworkManager.SignalName.MapLoaded);
            return;
        }

        NetworkManager.Instance.GameStarted += Start;

        // == player connected == //
        PlayerSpawnNode.ChildEnteredTree += (node) =>
        {
            if (node is PlayerCharacter player)
            {
                long id = long.Parse(player.Name);

                // == setting counter label to 0 == //
                PlayerScore.Add(id, 0);
                if (id == 1)
                {
                    UpdateKillCount(PlayerScore[id]);   // setting counter for server
                }
                else
                {
                    RpcId(id, nameof(UpdateKillCount), PlayerScore[id]);       // setting counter for client
                }

                RpcId(id, nameof(SetupLabel), (TimeLimit > 0) ? TimeLimit.ToString() : " ");

                // == on kill event == //
                player.HealthComponent.HealthDepleted += (instigator) =>
                {
                    if (!GameStarted || GameOver) return;
                    string killerName = GetTree().Root.GetNode(instigator).Name;
                    long id = long.Parse(killerName);

                    if (!PlayerScore.ContainsKey(id)) return;
                    PlayerScore[id]++;
                    if (id == 1) UpdateKillCount(PlayerScore[id]);              // if it is a server
                    else RpcId(id, nameof(UpdateKillCount), PlayerScore[id]);   // if it is a client
                    if (PlayerScore[id] == PointsCap && PointsCap > 0)
                    {
                        GD.Print("Reached kill cap");
                        DeathmatchTimer?.Dispose();
                        DeathmatchTimer = null;
                        EmitSignal(SignalName.GameEnded, id);
                    }
                };
            }
        };

        // == player disconnected == //
        PlayerSpawnNode.ChildExitingTree += (node) =>
        {
            long id = long.Parse(node.Name);
            if (PlayerScore.ContainsKey(id))
            {
                PlayerScore.Remove(id);
            }
        };

        GameEnded += (winner) => Rpc(nameof(EndGame), winner, PlayerScore);
        NetworkManager.Instance.EmitSignal(NetworkManager.SignalName.MapLoaded);
    }

    public override void _ExitTree()
    {
        NetworkManager.Instance.GameStarted -= Start;
        NetworkManager.Instance.RulesApplied -= SpawnWaitScreen;
    }

    // == timer starter == //
    private void StartTimer()
    {
        if (TimeLimit <= 0 || !Multiplayer.IsServer()) return;

        DeathmatchTimer = GetTree().CreateTimer(TimeLimit);
        DeathmatchTimer.Timeout += () =>
        {
            if (DeathmatchTimer == null) return;
            long winner = 1;
            int mostPoint = -1;
            foreach (var id in PlayerScore)
            {
                if (id.Value > mostPoint)
                {
                    winner = id.Key;
                }
            }
            EmitSignal(SignalName.GameEnded, winner);
        };

        Rpc(nameof(RemoteSetTimer));
    }

    // == timer label process == //
    private int LastSeconds = -1;
    public override void _Process(double delta)
    {
        if (DeathmatchTimer == null) return;

        int currentSeconds = Mathf.CeilToInt(DeathmatchTimer.TimeLeft);
        if(currentSeconds != LastSeconds)
        {
            TimerLabel.Text = currentSeconds.ToString() + "s";
            LastSeconds = currentSeconds;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void UpdateKillCount(int kills)
    {
        KillCounter.Text = kills.ToString() + ((PointsCap > 0) ? $"/{PointsCap}" : string.Empty);
    }

    // == timer set for clients == //
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)] 
    private void RemoteSetTimer() => DeathmatchTimer = GetTree().CreateTimer(TimeLimit);

    // == pre-game timer setup / base timer label remover == //
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SetupLabel(string time) => TimerLabel.Text = time;

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void EndGame(long winnerId, Dictionary<long,int> scoreboard)
    {
        DeathmatchTimer?.Dispose();
        DeathmatchTimer = null;
        GameOver = true;
        GameOverScreen gameoverScreen = GD.Load<PackedScene>(GameOverScreen.ScenePath).Instantiate() as GameOverScreen;
        gameoverScreen.Scoreboard = scoreboard;
        gameoverScreen.WinnerId = winnerId;
        GetTree().Root.AddChild(gameoverScreen);
        GD.Print($"{winnerId} has won!");
    }
}
