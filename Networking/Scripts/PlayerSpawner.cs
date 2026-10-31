using Godot;
using Godot.Collections;

public partial class PlayerSpawner : MultiplayerSpawner
{
    [Export] public PackedScene PlayerCharacter { get; private set; }
    [Export] public float TimeToRespawn { get; private set; } = 3f;
    [Export] public Array<NodePath> SpawnPoints { get; private set; }
    public Array<NodePath> AliveList { get; private set; } = [];

    public override void _Ready()
    {
        NetworkManager.Instance.GameStarted += SpawnSequence;
    }

    public override void _ExitTree()
    {
        NetworkManager.Instance.GameStarted -= SpawnSequence;
    }

    private void SpawnSequence()
    {
        SpawnPlayer(id: 1); // spawning host

        // spawning clients
        foreach (var playerId in Multiplayer.GetPeers())
        {
            SpawnPlayer(playerId);
        }
    }

    private Vector2 GetFarthestPoint()
    {
        if (SpawnPoints.Count == 0)
        {
            GD.Print("No spawn points found");
            return HelperFunctions.GetNodeFromPath(this, SpawnPath).GlobalPosition;
        }

        if (AliveList.Count == 0)
        {
            int randomInt = (int)(GD.Randi() % SpawnPoints.Count);
            Node2D randomPoint = HelperFunctions.GetNodeFromPath(this, SpawnPoints[randomInt]);
            GD.Print("Random spawn point");
            return randomPoint.GlobalPosition;
        }

        Node2D bestNode = HelperFunctions.GetNodeFromPath(this, SpawnPoints[0]);
        float greatestDistance = -1f;

        foreach (NodePath point in SpawnPoints)
        {
            float distanceToPlayer = float.MaxValue;
            Node2D pointNode = HelperFunctions.GetNodeFromPath(this, point);

            foreach (var player in AliveList)
            {
                Node2D playerNode = HelperFunctions.GetNodeFromPath(this, player);
                float distance = pointNode.GlobalPosition.DistanceSquaredTo(playerNode.GlobalPosition);
                if (distance < distanceToPlayer) distanceToPlayer = distance;
            }

            if (distanceToPlayer > greatestDistance)
            {
                greatestDistance = distanceToPlayer;
                bestNode = pointNode;
            }
        }

        return bestNode.GlobalPosition;
    }

    public void SpawnPlayer(long id)
    {
        if (!Multiplayer.IsServer()) return;

        PlayerCharacter player = PlayerCharacter.Instantiate() as PlayerCharacter;
        player.Name = id.ToString();
        GetNode(SpawnPath).AddChild(player);
        Rpc(nameof(RelocatePlayer), player.GetPath(), GetFarthestPoint());
        AliveList.Add(player.GetPath());
        player.HealthComponent.HealthDepleted += (_) =>
        {
            AliveList.Remove(player.GetPath());
            GetTree().CreateTimer(TimeToRespawn).Timeout += () =>
            {
                Rpc(nameof(RelocatePlayer), player.GetPath(), GetFarthestPoint());
                AliveList.Add(player.GetPath());
                player.HealthComponent.Revive();
            };
        };
        GD.Print($"Spawned {player.Name}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RelocatePlayer(NodePath playerPath, Vector2 newPosition)
    {
        GetNode<Node2D>(playerPath).GlobalPosition = newPosition;
    }
}
