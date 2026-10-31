using Godot;

public static class HelperFunctions
{
    public static Node2D GetNodeFromPath(Node parent, NodePath path) => parent.GetNode(path) as Node2D;

    // == for shots debug == //
    public static void DrawLine(Node parent, Vector2 start, Vector2 end, Color color, float duration = 1.0f) // needs an explanation
    {
        var line = new Line2D();
        parent.AddChild(line);

        line.Width = 2.0f;
        line.DefaultColor = color;
        line.AddPoint(start);
        line.AddPoint(end);

        Tween tween = line.CreateTween();

        tween.TweenInterval(duration * 0.5f);
        tween.TweenProperty(line, "modulate:a", 0.0f, duration * 0.5f);

        tween.Finished += line.QueueFree;
    }

    // == for hits debug == //
    public static void DrawHitMarker(Node parent, Vector2 position, float size, Color color, float duration = 1.0f)
    {
        var marker = new Node2D();
        marker.Position = position;
        parent.AddChild(marker);

        float half = size / 2;
        Vector2[] line1Points = { new Vector2(-half, -half), new Vector2(half, half) };
        Vector2[] line2Points = { new Vector2(half, -half), new Vector2(-half, half) };

        CreateDebugLine(marker, line1Points, color);
        CreateDebugLine(marker, line2Points, color);

        Tween tween = marker.CreateTween().SetParallel(true);

        tween.TweenProperty(marker, "modulate:a", 0.0f, duration);
        tween.TweenProperty(marker, "scale", new Vector2(1.5f, 1.5f), duration);

        tween.Chain().TweenCallback(Callable.From(() => marker.QueueFree()));
    }
    private static void CreateDebugLine(Node parent, Vector2[] points, Color color)
    {
        var line = new Line2D();
        line.Points = points;
        line.Width = 2.0f;
        line.DefaultColor = color;
        parent.AddChild(line);
    }
}
