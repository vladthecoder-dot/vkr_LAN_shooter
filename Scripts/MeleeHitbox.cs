using Godot;
using Godot.Collections;

public partial class MeleeHitbox : Area2D
{
    private float _originalWidth;
    private RectangleShape2D _rectangleMelee;

    [Signal] public delegate void SendHitsEventHandler(Array<Node> players);
    [Export] public float TimeToLive { get; private set; } = 0.1f;
    [Export] public CollisionShape2D MeleeShape { get; private set; }
    public Array<Node> Hits { get; private set; } = [];

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

    public override void _Ready()
    {
        if (!Multiplayer.IsServer()) return;
        // == hit event == //
        BodyEntered += (node) =>
        {
            GD.Print(node.Name);
            if (IgnoreList.Contains(node))
            {
                return;
            }
            else if (!Hits.Contains(node))
            {
                Hits.Add(node);
            }  
        };

        // == shrink == //
        _rectangleMelee = MeleeShape.Shape as RectangleShape2D;
        _originalWidth = _rectangleMelee.Size.X;
        _rectangleMelee.Size = new Vector2(x: 0, _rectangleMelee.Size.Y);

        // == regrow == //
        Tween tween = CreateTween();
        tween.TweenProperty(_rectangleMelee, "size:x", _originalWidth, TimeToLive);
        tween.Finished += () =>
        {
            EmitSignal(SignalName.SendHits, Hits);
            QueueFree();
        };
    }
}
