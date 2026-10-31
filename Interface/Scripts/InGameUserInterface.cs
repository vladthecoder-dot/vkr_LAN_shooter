using Godot;
using Godot.Collections;

public partial class InGameUserInterface : Control
{
    public static readonly string PackedScenePath = "res://Interface/Scenes/InGameUserInterface.tscn";
    public static readonly Dictionary<string, string> ActionsTips = new()
    {
        { UserOptions.AttackUI, "Attack" },
        { UserOptions.ThrowUI, "Throw" },
        { UserOptions.MeleeUI, "Punch" },
        { UserOptions.SlideUI, "Slide" },
        { UserOptions.BackflipUI, "Backflip" },
        { UserOptions.JumpUI, "Jump" },

    };

    [Export] public TextureButton DisconnectButton { get; private set; }
    [Export] public Label UserInputTips { get; private set; }

    public override void _Ready()
    {
        Multiplayer.ServerDisconnected += QueueFree;
        UserInputTips.Text = GetUserInputTips();
        DisconnectButton.Pressed += async() =>
        {
            await NetworkManager.Instance.DisconnectFromGame();
            QueueFree();
        };
            
    }

    public static string GetUserInputTips()
    {
        string tip = string.Empty;
        foreach (var action in ActionsTips)
        {
            tip += $"{UserOptions.GetActionKeyName(action.Key)}-{action.Value}   ";
        }
        return tip;
    }

    public override void _ExitTree()
    {
        Multiplayer.ServerDisconnected -= QueueFree;
    }

}
