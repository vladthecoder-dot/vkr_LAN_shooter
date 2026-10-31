using Godot;
using System;

public partial class WeaponPickup : Area2D
{
    private PackedScene _weaponScene;
    public PackedScene WeaponScene 
    {
        get => _weaponScene;
        set
        {
            if (IsInsideTree()) return;
            _weaponScene = value;
        }
    }

    [Export] public Sprite2D PickupSprite { get; private set; }
    [Signal] public delegate void ItemPickedEventHandler();

    public override void _Ready()
    {
        BodyEntered += OnPlayerEntered;
    }

    private void OnPlayerEntered(Node body)
    {
        if(body is PlayerCharacter character)
        {
            if (!character.IsMultiplayerAuthority()) return;
            Rpc(nameof(RequestPickup), character.GetPath());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RequestPickup(NodePath playerPath)
    {
        if (!Multiplayer.IsServer()) return;
        PlayerCharacter character = GetNode<PlayerCharacter>(playerPath);
        if (character == null || character.Weapon != null) return;
        // =?= maybe add anti-cheat check =?= //
        Node weapon = WeaponScene.Instantiate();
        if(weapon is IWeapon weaponInterface)
        {
            weaponInterface.PlayerOwner = character;
        }
        Node holder = character.GetNode<Node>(character.WeaponHolder);
        holder.AddChild(weapon);
        Rpc(nameof(PickupFeedback), playerPath);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void PickupFeedback(NodePath playerPath)
    {
        EmitSignal(SignalName.ItemPicked);
        QueueFree();
    }
}
