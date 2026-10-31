using Godot;
using Godot.Collections;

public partial class Projectile : Area2D
{
    [Export] public float MaximumSpeed { get; private set; } = 500f;
    [Export] public Sprite2D Sprite { get; private set; }
    public Vector2 ProjectileVelocity { get; private set; }
    public float ProjectileGravity { get; private set; }

    private Vector2 _direction;
    public Vector2 Direction
    {
        get => _direction;
        set
        {
            if (IsInsideTree()) return;
            _direction = value - new Vector2(0,0.5f);
        }
    }

    private Array<Node> _ignoreList = [];
    public Array<Node> IgnoreList 
    {
        get => _ignoreList;
        set
        {
            if (IsInsideTree()) return;
            _ignoreList = value;
        }
    }

    private int _damage;
    public int Damage
    {
        get => _damage;
        set
        {
            if (IsInsideTree()) return;
            _damage = value;
        }
    }

    private PlayerCharacter _playerOwner;
    public PlayerCharacter PlayerOwner
    {
        get => _playerOwner;
        set
        {
            if(IsInsideTree()) return;
            _playerOwner = value;
        }
    }

    public override void _Ready()
    {
        ProjectileGravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
        ProjectileVelocity = Direction * MaximumSpeed;

        if (!Multiplayer.IsServer()) return;

        IgnoreList.Add(PlayerOwner);

        BodyEntered += (body) =>
        {
            if (IgnoreList.Contains(body)) return;
            IWeapon.ApplyDamage(Damage, body, PlayerOwner.GetPath());
            CallDeferred(Node.MethodName.QueueFree);
            //QueueFree();
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Multiplayer.IsServer())
        {
            RPC_MovementHandler();
        }
        else
        {
            MovementHandler((float)delta);
        }
    }

    private void MovementHandler(float delta)
    {
        GlobalPosition += ProjectileVelocity * delta;
        float velocityX = Mathf.MoveToward(from:ProjectileVelocity.X, to:0, delta);
        ProjectileVelocity = new Vector2(velocityX, ProjectileVelocity.Y + ProjectileGravity * delta);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RPC_MovementHandler()
    {
        MovementHandler((float)GetPhysicsProcessDeltaTime());
        Rpc(nameof(UpdateState), GlobalPosition, ProjectileVelocity);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void UpdateState(Vector2 position, Vector2 velocity)
    {
        GlobalPosition = position;
        ProjectileVelocity = velocity;
    }
}
