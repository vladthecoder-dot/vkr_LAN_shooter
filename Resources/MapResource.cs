using Godot;

[GlobalClass]
public partial class MapResource : Resource
{
    [Export] public string Name { get; private set; }
    [Export] public Texture2D Preview { get; private set; }
    [Export] public string MapScenePath { get; private set; }
    [Export] public int PlayersCap { get; private set; } // must be <= to count of spawn point on the map
}
