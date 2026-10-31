using Godot;

[GlobalClass]
public partial class HealthComponent : Node
{
    [Export] public int MaxHealth { get; private set; } = 10;
    [Export] public Node2D Visuals { get; private set; }
    public int CurrentHealth { get; private set; }

    [Export] public bool Alive { get; private set; } = true;

    [Signal] public delegate void HealthDepletedEventHandler(NodePath instigator);
    [Signal] public delegate void HealthChangedEventHandler(int previousValue);
    [Signal] public delegate void OwnerRevivedEventHandler();

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SignalDepletion(NodePath cause) => EmitSignal(SignalName.HealthDepleted, cause);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SignalRevival() => EmitSignal(SignalName.OwnerRevived);

    public override void _Ready()
    {
        Owner = GetParent();
        if (Owner.IsInGroup("players")) MaxHealth = (int)NetworkManager.Rules["Health"];
        CurrentHealth = MaxHealth;
        if (Owner.HasNode("HealthBar"))
        {
            GD.Print("Health bar detected");
            ProgressBar healthBar = Owner.GetNode<ProgressBar>("HealthBar");
            healthBar.MaxValue = MaxHealth;
            healthBar.Value = CurrentHealth;
            if (healthBar.HasNode("HealthLabel"))
            {
                Label healthLabel = healthBar.GetNode<Label>("HealthLabel");
                healthLabel.Text = $"{CurrentHealth}/{MaxHealth}";
                HealthChanged += (_) =>
                {
                    healthLabel.Text = $"{CurrentHealth}/{MaxHealth}";
                };
                OwnerRevived += () => healthLabel.Text = $"{CurrentHealth}/{MaxHealth}";
            }
            HealthChanged += (_) => healthBar.Value = CurrentHealth;
            OwnerRevived += () => healthBar.Value = MaxHealth;
        }
        if (!Multiplayer.IsServer()) return;
        HealthChanged += (previous) =>
        {
            if (!Multiplayer.IsServer() || !Alive) return;
            Rpc(nameof(UpdateHealth), CurrentHealth, previous);
        };
    }

    public void TakeDamage(int damage, NodePath instigator)
    {
        if (!Multiplayer.IsServer() || !Alive) return;
        int healthBeforeDamage = CurrentHealth;
        CurrentHealth -= damage;
        GD.Print($"Damage taken");
        EmitSignal(SignalName.HealthChanged, healthBeforeDamage);
        if (CurrentHealth <= 0)
        {
            Alive = false;
            Rpc(nameof(SignalDepletion), instigator);
        }
    }

    public void Revive()
    {
        if (!Multiplayer.IsServer() || Alive) return;
        CurrentHealth = MaxHealth;
        Alive = true;
        Rpc(nameof(UpdateHealth), MaxHealth, 0);
        Rpc(nameof(SignalRevival));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void UpdateHealth(int newHealth, int valueChange)
    {
        int preUpdateHealth = CurrentHealth;   
        CurrentHealth = newHealth;
        EmitSignal(SignalName.HealthChanged, preUpdateHealth);
    }

    //[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    //private void SwitchAliveState(bool state)
    //{
    //    Alive = state;
    //}
}
