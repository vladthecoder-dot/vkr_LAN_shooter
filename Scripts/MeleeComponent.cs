using Godot;
using System;

public partial class MeleeComponent : Node, IAttackStats, IWeapon
{
    [Export] public PackedScene Hitbox { get; private set; }
    [Export] public float AttackCooldown { get; private set; } = 1f;
    [Export] public int Damage { get; private set; } = 3;
    [Export] public NodePath SpawnPath { get; private set; }
    [Export] public int Durability { get; private set; } = 0;

    [Export] public float SlideToss { get; private set; } = 300f;
    [Export] public float BackflipToss { get; private set; } = 500f;
    [Export] public AudioStreamPlayer MeleeSound { get; private set; }

    public bool CanAttack { get; private set; } = true;
    public PlayerCharacter PlayerOwner { get; set; }
    public Action WeaponDeleted { get; set; }
    [Export] public PackedScene ThrownWeaponScene { get; private set; }
    public SceneTreeTimer PunchCooldown { get; private set; }

    public override void _Ready()
    {
        Damage = (int)NetworkManager.Rules["MeleeDamage"];
        PlayerOwner = Owner as PlayerCharacter;
    }

    public void Attack()
    {
        if (!Multiplayer.IsServer() || !CanAttack) return;

        MeleeHitbox hitbox = Hitbox.Instantiate<MeleeHitbox>();
        MeleeSound.Play();
        hitbox.SendHits += (nodes) =>
        {
            foreach(var hit in nodes)
            {
                IWeapon.ApplyDamage(Damage, hit, PlayerOwner.GetPath());
                if (hit is PlayerCharacter player) MeleeToss(player);
            }
        };
        hitbox.IgnoreList.Add(GetParent());
        GetNode(SpawnPath).AddChild(hitbox);
        CanAttack = false;
        MeleeFeedback();
        RpcId(PlayerOwner.GetMultiplayerAuthority(), nameof(MeleeFeedback));
    }

    public void AlternativeAttack() { }

    private void MeleeToss(PlayerCharacter player)
    {
        switch(PlayerOwner.State)
        {
            case CharacterState.Sliding: player.Toss(SlideToss); break;
            case CharacterState.Backfliping: player.Toss(BackflipToss); break;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void MeleeFeedback()
    {
        PunchCooldown = GetTree().CreateTimer(AttackCooldown);
        PunchCooldown.Timeout += () =>
        {
            CanAttack = true;
        };
    }
}
