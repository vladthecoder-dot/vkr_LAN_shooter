using Godot;
using System;
using System.Diagnostics.Metrics;

public partial class Gun : Node2D, IWeapon, IAttackStats
{
    private PlayerCharacter _playerOwner;
    public PlayerCharacter PlayerOwner 
    {
        get => _playerOwner;
        set
        {
            if (IsInsideTree()) return;
            _playerOwner = value;
        }
    }
    
    [Export] public float ProjectileY { get; private set; } = -0.5f;
    [Export] public Node2D MuzzleNode { get; private set; }
    public Vector2 MuzzlePosition
    {
        get => MuzzleNode.GlobalPosition;
    }
    public Vector2 ForwardVector
    {
        get => new Vector2(1, 0) * PlayerOwner.Facing;
    }

    private float _attackCooldown = 1f;
    [Export] public float AttackCooldown 
    {
        get
        {
            switch (PlayerOwner.State)
            {
                case CharacterState.Normal: return _attackCooldown;
                case CharacterState.Backfliping: return _attackCooldown/2;
                case CharacterState.Sliding: return _attackCooldown/1.25f;
                default: return _attackCooldown;
            }
        }
        private set => _attackCooldown = value;
    }

    [Export] public int Damage { get; private set; } = 2;
    [Export] public int Durability { get; private set; } = 6;
    [Export] public PackedScene ThrownWeaponScene { get; private set; }
    public bool CanAttack { get; private set; } = true;
    public Action WeaponDeleted { get; set; }

    // == visuals == //
    [Export] public AnimatedSprite2D WeaponSprite { get; private set; }
    [Export] public SpriteFrames HitFrames { get; private set; }

    // == sounds == //
    [Export] public AudioStreamPlayer SpawnSound { get; private set; }
    [Export] public AudioStreamPlayer ShotSound { get; private set; }

    public override void _Ready()
    {
        Damage = (int)NetworkManager.Rules["GunDamage"];
        Durability = (int)NetworkManager.Rules["GunAmmo"];
        SpawnSound.Play();
    }

    // == simple shots == //
    public void Attack()
    {
        if (!Multiplayer.IsServer()) return;

        if (!CanAttack || Durability == 0) return;
        var spaceState = GetWorld2D().DirectSpaceState;
        Vector2 endPosition = MuzzlePosition + (ForwardVector * 2000f);
        Vector2 shotEnd = endPosition;
        bool didHit = false;

        var query = PhysicsRayQueryParameters2D.Create(MuzzlePosition, endPosition);
        var result = spaceState.IntersectRay(query);
        CanAttack = false;
        GetTree().CreateTimer(AttackCooldown).Timeout += () => 
        {
            CanAttack = true;
        };

        if(result.Count > 0)
        {
            didHit = true;
            shotEnd = (Vector2)result["position"];
            Node hit = (Node)result["collider"];
            IWeapon.ApplyDamage(Damage, hit, PlayerOwner.GetPath());

        }
        GD.Print($"Muzzle position {MuzzlePosition}");

        Rpc(nameof(AttackFeedback), MuzzlePosition, shotEnd, didHit, PlayerOwner.Facing);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void AttackFeedback(Vector2 shotStart, Vector2 shotEnd, bool hitSomething, int facing)
    {
        Durability--;
        ShotSound.Play();
        //HelperFunctions.DrawLine(GetTree().Root, shotStart, shotEnd, Colors.Red);
        WeaponSprite.Play();
        if(hitSomething)
        {
            var hitSprite = new AnimatedSprite2D();
            hitSprite.SpriteFrames = HitFrames;
            GetTree().Root.AddChild(hitSprite);
            hitSprite.GlobalPosition = shotEnd;
            hitSprite.Scale = new Vector2(facing, y:1);
            hitSprite.AnimationFinished += hitSprite.QueueFree;
            hitSprite.Play();
            //HelperFunctions.DrawHitMarker(GetTree().Root, shotEnd, 15, Colors.Green);
        }
    }

    // == weapon throw == //
    public void AlternativeAttack()
    {
        if (!Multiplayer.IsServer()) return;
        Projectile thrownWeapon = ThrownWeaponScene.Instantiate() as Projectile;
        thrownWeapon.Direction = ForwardVector + new Vector2(x:0, ProjectileY);
        thrownWeapon.Damage = Damage;
        thrownWeapon.PlayerOwner = PlayerOwner;
        IGameModeManager.Instance.ProjectileSpawnPoint.AddChild(thrownWeapon, true);
        Rpc(nameof(AltAttackFeedback), thrownWeapon.GetPath(), MuzzlePosition);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void AltAttackFeedback(NodePath projectilePath, Vector2 position)
    {
        Projectile projectile = GetTree().Root.GetNode<Projectile>(projectilePath);
        projectile.GlobalPosition = position;
        projectile.Sprite.Texture = WeaponSprite.SpriteFrames.GetFrameTexture(WeaponSprite.Animation, idx:0);
        WeaponDeleted.Invoke();
        QueueFree();
    }
}
