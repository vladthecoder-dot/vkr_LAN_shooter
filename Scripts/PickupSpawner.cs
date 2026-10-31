using Godot;
using Godot.Collections;

public partial class PickupSpawner : MultiplayerSpawner
{
    [Export] protected Array<NodePath> SpawnPoints { get; private set; } = [];
    [Export] public PackedScene PickupItem { get; private set; }
    [Export] public Texture2D PickupSprite { get; private set; }
    [Export] public PackedScene PickupScene { get; private set; }
    [Export] public float TimeToSpawn { get; private set; } = 5f;

    // =!= do some work here =!= //
    protected Dictionary<Node, NodePath> PickupToLocation = [];
    protected Array<NodePath> VacantSpawnPoints { get; private set; }

    public override void _Ready()
    {
        VacantSpawnPoints = SpawnPoints;
    }

    public void TurnSpawnerOn()
    {
        if (!Multiplayer.IsServer()) return;
        SpawnByTimer();
        // =!= late joiners feature =!= //
        //Multiplayer.PeerConnected += (id) =>
        //{
        //    foreach(var pickupPath in Pickups)
        //    {
        //        RpcId(id, nameof(RelocatePickup), pickupPath, GetNode<WeaponPickup>(pickupPath).GlobalPosition);
        //    }
        //};
    }

    private void SpawnByTimer()
    {
        GetTree().CreateTimer(TimeToSpawn).Timeout += () =>
        {
            SpawnPickup();
            SpawnByTimer();
        };
    }

    private Node SpawnPickup()
    {
        if(!Multiplayer.IsServer() || VacantSpawnPoints.Count == 0) return null;
        WeaponPickup pickup = PickupScene.Instantiate() as WeaponPickup;
        pickup.WeaponScene = PickupItem;
        GetNode(SpawnPath).AddChild(pickup, true);

        int randomInt = (int)(GD.Randi() % VacantSpawnPoints.Count);
        Node2D randomNode = HelperFunctions.GetNodeFromPath(this, VacantSpawnPoints[randomInt]);
        PickupToLocation.Add(pickup, VacantSpawnPoints[randomInt]);
        VacantSpawnPoints.RemoveAt(randomInt);
        pickup.ItemPicked += () =>
        {
            VacantSpawnPoints.Add(PickupToLocation[pickup]);
            PickupToLocation.Remove(pickup);
        };

        Rpc(nameof(RelocatePickup), pickup.GetPath(), randomNode.GlobalPosition);
        return pickup;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RelocatePickup(NodePath pickupPath, Vector2 newPosition)
    {
        WeaponPickup pickup = GetNode<WeaponPickup>(pickupPath);
        pickup.GlobalPosition = newPosition;
        pickup.PickupSprite.Texture = PickupSprite;
    }
}
