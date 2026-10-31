using Godot;

public partial class FpsCounter : Label
{
    private const double _timeStep = 1;
    private double _timeSkipped = 0;

    public override void _Ready()
    {
        Hide();
        SetAnchorsPreset(LayoutPreset.TopLeft);
    }

    public override void _Process(double delta)
    {
        _timeSkipped += delta;
        if (_timeSkipped >= _timeStep)
        {
            Text = $"FPS: {Engine.GetFramesPerSecond()}";
            _timeSkipped = 0;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_show_fps"))
        {
            Visible = !Visible;
            MoveToFront();
        }
    }
}
