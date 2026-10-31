using Godot;
using System;

public interface IWeapon
{
    public void Attack();
    public void AlternativeAttack();
    public PlayerCharacter PlayerOwner { get; set; }
    public PackedScene ThrownWeaponScene { get; }
    public Action WeaponDeleted { get; set;  }
    public static void ApplyDamage(int damage, Node target, NodePath instigator)
    {
        if (target.HasNode("HealthComponent") && target.Multiplayer.IsServer())
        {
            target.GetNode<HealthComponent>("HealthComponent").TakeDamage(damage, instigator);
            GD.Print($"{target.Name} got {damage} points of damage");
        }
    }
}

public interface IAttackStats
{
    public int Damage { get; }
    public float AttackCooldown { get; }
    public bool CanAttack { get; }
    public int Durability { get; }
}