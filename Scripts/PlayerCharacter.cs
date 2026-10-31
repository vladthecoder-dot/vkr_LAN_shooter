using Godot;
using System;

public enum CharacterState
{
    Dead,
    Normal,
    Sliding,
    Backfliping
}

public partial class PlayerCharacter : CharacterBody2D
{
    // == internal variables == //
    private float _backflipTime = 0f;
    public float Gravity { get; private set; }

    // == constant tables == //
    private System.Collections.Generic.Dictionary<string, Action> _inputs;
    private System.Collections.Generic.Dictionary<CharacterState, CollisionShape2D> _stateCollisions;

    // == visuals == //
    [Export] public Node2D CharacterVisuals { get; private set; }
    [Export] public Sprite2D AuthorityMarkerSprite { get; private set; }
    [Export] public Sprite2D CharacterSprite { get; private set; }
    [Export] public Sprite2D SlidingSprite { get; private set; }
    public int Facing { get; private set; } = 1; // facing right when spawned

    // == sounds == //
    [Export] public AudioStreamPlayer ReviveSound { get; private set; }
    [Export] public AudioStreamPlayer JumpSound { get; private set; }

    // == weapon == //
    [Export] public NodePath WeaponHolder { get; private set; }
    [Export] public MeleeComponent Melee { get; private set; }
    public IWeapon Weapon { get; private set; }

    // == health == //
    [Export] public HealthComponent HealthComponent { get; private set; }

    // == state machine == //
    private CharacterState _state;
    public CharacterState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            Visible = _state != CharacterState.Dead;
            CharacterSprite.Visible = _state != CharacterState.Sliding;
            SlidingSprite.Visible = _state == CharacterState.Sliding;
            foreach (var collision in _stateCollisions)
            {
                bool active = collision.Key == _state;
                collision.Value.SetDeferred(CollisionShape2D.PropertyName.Disabled, !active);
                collision.Value.Visible = active;
            }
        }
    }

    // == coyote time == //
    private float _canCoyoteTimer = 0;
    public bool CanCoyote => (_canCoyoteTimer <= CoyoteTime);

    // == movement parameters == //
    [Export] public float Speed { get; private set; } = 250f;
    [Export] public float JumpVelocity { get; private set; } = 400f;
    [Export] public float CoyoteTime { get; private set; } = 0.1f;

    [Export] public float BackflipSlowdown { get; private set; } = 100f;
    [Export] public float BackflipRotationRate { get; private set; } = 360f;
    [Export] public float BackflipVerticalVelocity { get; private set; } = 300f;
    [Export] public float BackflipHorizontalVelocity { get; private set; } = 300f;

    [Export] public float SlideSlowdown { get; private set; } = 100f;
    [Export] public float SlideVelocity { get; private set; } = 300f;
    [Export] public float SlideStopSpeed { get; private set; } = 10f;

    public override void _Ready()
    {
        Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
        AddToGroup("players");
        _inputs = new()
        {
            {"ui_cancel", () => GetTree().Quit()},
            {"ui_jump", () => SendAndPredict(nameof(RPC_JumpInput), JumpInput)},
            {"ui_backflip", () => SendAndPredict(nameof(RPC_BackflipInput), BackflipInput)},
            {"ui_slide", () => SendAndPredict(nameof(RPC_SlideInput), SlideInput)},
            {"ui_fire", AttackInput},
            {"ui_alt_fire", AltAttackInput},
            {"ui_punch", PunchAttackInput},

        };
        _stateCollisions = new()
        {
            {CharacterState.Normal, GetNode<CollisionShape2D>("NormalCollision")},
            {CharacterState.Backfliping, GetNode<CollisionShape2D>("BackflipCollision")},
            {CharacterState.Sliding, GetNode<CollisionShape2D>("SlidingCollision")},
        };
        Speed = (float)NetworkManager.Rules["Speed"];

        // == weapon holder init == //
        Node weaponHolder = GetNode<Node>(WeaponHolder);
        weaponHolder.ChildEnteredTree += (child) =>
        {
            if (child is IWeapon newWeapon)
            {
                Weapon = newWeapon;
                newWeapon.WeaponDeleted += () =>
                {
                    Weapon = null;
                };
            }
        };

        // == health component init == //
        State = CharacterState.Normal;
        HealthComponent.HealthDepleted += (_) =>
        {
            State = CharacterState.Dead;
            Velocity = Vector2.Zero;
            if (Weapon != null && Multiplayer.IsServer())
            {
                (Weapon as Node).QueueFree();
                Weapon = null;
            }
        };
        HealthComponent.OwnerRevived += () =>
        {
            State = CharacterState.Normal;
            ReviveSound.Play();
        };
        Multiplayer.PeerDisconnected += (id) =>
        {
            if (long.Parse(Name) == id)
            {
                QueueFree();
            }
        };

        // == gui init == //
        AuthorityMarkerSprite.Hide();
        GetNode<Label>("PlayerName").Text = NetworkManager.PeerNames[GetMultiplayerAuthority()];

        if (GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            AuthorityMarkerSprite.Show();
            Node ui = GD.Load<PackedScene>(InGameUserInterface.PackedScenePath).Instantiate();
            GetTree().Root.AddChild(ui);
        }
    }

    public override void _EnterTree()
    {
        SetMultiplayerAuthority(int.Parse(Name), false);
    }

    // == attack == //
    private void AttackInput()
    {
        if (!IsMultiplayerAuthority()) return;
        Rpc(nameof(RPC_Attack));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void RPC_Attack()
    {
        if (!Multiplayer.IsServer()) return;
        if (Weapon != null) Weapon.Attack();
        else Melee.Attack();
    }

    // == alt attack == //
    private void AltAttackInput()
    {
        if (!IsMultiplayerAuthority()) return;
        Rpc(nameof(RPC_AltAttack));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void RPC_AltAttack()
    {
        if (!Multiplayer.IsServer()) return;
        Weapon?.AlternativeAttack();
    }

    // == punch == //
    private void PunchAttackInput()
    {
        if (!IsMultiplayerAuthority()) return;
        Rpc(nameof(RPC_PunchAttack));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void RPC_PunchAttack()
    {
        if (!Multiplayer.IsServer()) return;
        Melee.Attack();
    }

    // == backflip == //
    private void BackflipInput()
    {
        if (!CanCoyote) return;
        State = CharacterState.Backfliping;
        Velocity = new Vector2(-Facing * BackflipHorizontalVelocity, -BackflipVerticalVelocity);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_BackflipInput()
    {
        BackflipInput();
        UpdateMovementRPC();
    }

    private void BackflipLanded() // rpc is not needed as it is called in replicated handler
    {
        State = CharacterState.Normal;
        CharacterSprite.RotationDegrees = 0f;
    }

    private void BackflipHandler(float delta)
    {
        if (State == CharacterState.Backfliping)
        {
            CharacterSprite.RotationDegrees -= BackflipRotationRate * delta;
            float xVelocity = Mathf.MoveToward(from: Velocity.X, to: 0, BackflipSlowdown * delta);
            Velocity = new Vector2(xVelocity, Velocity.Y);
            _backflipTime += delta;
            if (IsOnFloor() && _backflipTime > 0.2)
            {
                BackflipLanded();
                _backflipTime = 0;
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_BackflipHandler()
    {
        BackflipHandler((float)GetPhysicsProcessDeltaTime());
        UpdateMovementRPC();
    }

    // == slide == //
    private void SlideInput()
    {
        if (State != CharacterState.Normal || !CanCoyote) return;

        State = CharacterState.Sliding;
        Velocity = new Vector2(Facing * SlideVelocity, Velocity.Y);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_SlideInput()
    {
        SlideInput();
        UpdateMovementRPC();
    }

    private void StopSlide() // rpc is not needed as it is called in replicated handler
    {
        State = CharacterState.Normal;
    }

    private void SlideHandler(float delta)
    {
        if (State == CharacterState.Sliding)
        {
            float xVelocity = Mathf.MoveToward(from: Velocity.X, to: 0, SlideSlowdown * delta);
            Velocity = new Vector2(xVelocity, Velocity.Y);
            if (MathF.Abs(Velocity.X) <= SlideStopSpeed)
            {
                StopSlide();
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_SlideHandler()
    {
        SlideHandler((float)GetPhysicsProcessDeltaTime());
        UpdateMovementRPC();
    }

    // == normal movement == //
    private void NormalInputHandler(float direction)
    {
        Velocity = new Vector2(direction * Speed, Velocity.Y);
        if (direction != 0)
        {
            Facing = direction > 0 ? 1 : -1;
            CharacterVisuals.Scale = new Vector2(Facing, 1);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_NormalInputHandler(float direction)
    {
        NormalInputHandler(direction);
        UpdateMovementRPC();
    }

    // == gravity == //
    private void GravityHandler(float delta)
    {
        if (IsOnFloor()) return;
        Velocity += new Vector2(0, Gravity * delta);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_GravityHandler()
    {
        GravityHandler((float)GetPhysicsProcessDeltaTime());
        UpdateMovementRPC();
    }

    // == jump input == // 
    private void JumpInput()
    {
        if (!CanCoyote || State != CharacterState.Normal) return;
        _canCoyoteTimer = CoyoteTime;
        Velocity = new Vector2(Velocity.X, -JumpVelocity);
        JumpSound.Play();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_JumpInput()
    {
        JumpInput();
        UpdateMovementRPC();
    }

    // == toss == //
    public void Toss(float yVelocity)
    {
        if (!Multiplayer.IsServer()) return;
        Velocity = new Vector2(x:0, -yVelocity);
        UpdateMovementRPC();
    }

    // == rpc for MoveAndSlide func == //
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RPC_MoveAndSlide()
    {
        if (!HealthComponent.Alive) return;
        MoveAndSlide();
        UpdateMovementRPC();
    }

    // == input and gravity process == //
    public override void _PhysicsProcess(double delta)
    {
        if (!HealthComponent.Alive) return;

        // == input detection == //
        if (IsMultiplayerAuthority())
        {
            foreach (var pair in _inputs)
            {
                if (Input.IsActionJustPressed(pair.Key)) pair.Value.Invoke();
            }
        }

        // == coyote time ticking == //
        if (!IsOnFloor())
        {
            _canCoyoteTimer += (float)delta;
        }
        else
        {
            _canCoyoteTimer = 0;
        }

            // == input actions == //
        SendAndPredict(nameof(RPC_GravityHandler), () => GravityHandler((float)delta));
        switch (State)
        {
            case CharacterState.Normal:
                float direction = Input.GetAxis("ui_left", "ui_right");
                SendAndPredict(nameof(RPC_NormalInputHandler), () => NormalInputHandler(direction), direction);
                break;
            case CharacterState.Backfliping:
                SendAndPredict(nameof(RPC_BackflipHandler), () => BackflipHandler((float)delta));
                break;
            case CharacterState.Sliding:
                SendAndPredict(nameof(RPC_SlideHandler), () => SlideHandler((float)delta));
                break;
        }
        // == network correction == //
        if (!(IsMultiplayerAuthority() || Multiplayer.IsServer()))
        {
            Velocity = Velocity.Lerp(_networkVelocity, 1f);
            GlobalPosition = GlobalPosition.Lerp(_networkPosition, 1f);
        }
        SendAndPredict(nameof(RPC_MoveAndSlide), () => MoveAndSlide());
    }

    // == networking == //
    private Vector2 _networkPosition, _networkVelocity;

    private void UpdateMovementRPC()
    {
        if(HealthComponent.Alive && IsInsideTree()) Rpc(nameof(UpdateState), GlobalPosition, Velocity);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void UpdateState(Vector2 position, Vector2 velocity)
    {
        _networkPosition = position;
        _networkVelocity = velocity;
    }    

    private void SendAndPredict(string rpcMethodName, Action prediction, params Variant[] args)
    {
        if (!IsMultiplayerAuthority() || !HealthComponent.Alive) return;
        if (Multiplayer.IsServer())
        {
            Call(rpcMethodName, args);
            return;
        }

        prediction?.Invoke();
        Rpc(rpcMethodName, args);
    }
}
