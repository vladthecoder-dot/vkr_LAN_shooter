using Godot;
using System;
using System.Threading.Tasks;

public partial class Message : Control
{
    public const string ScenePath = "res://Interface/Scenes/Message.tscn";

    [Export] public Button AcceptButton { get; private set; }
    [Export] public Label MessageLabel { get; private set; }

    [Signal] public delegate void MessageAcceptedEventHandler();

    public override async void _Ready()
    {
        AcceptButton.Pressed += () =>
        {
            EmitSignal(SignalName.MessageAccepted);
            QueueFree();
        };

        await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
        MoveToFront();
    }
}
