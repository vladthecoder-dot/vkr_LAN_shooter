using Godot;
using Godot.Collections;

public partial class GameCreationMenu : Control
{
    [Export] public SpinBox TimeCapSpin { get; private set; }
    [Export] public CheckBox TimerEnabled { get; private set; }

    [Export] public SpinBox PointsCapSpin { get; private set; }
    [Export] public CheckBox KillCapEnabled { get; private set; }

    [Export] public SpinBox MaximumPlayersSpin { get; private set; }
    [Export] public Button StartButton { get; private set; }

    [Export] public SpinBox MaximumHealthSpin { get; private set; }
    [Export] public SpinBox SpeedSpin { get; private set; }

    [Export] public SpinBox GunDamageSpin { get; private set; }
    [Export] public SpinBox GunAmmoSpin { get; private set; }
    [Export] public SpinBox MeleeDamageSpin { get; private set; }

    [Export] public Button BackButton { get; private set; }

    [Export] public Container MapTilesBox { get; private set; }
    [Export] public PackedScene MapTile { get; private set; }
    [Export] public Array<MapResource> MapRoster { get; private set; } = [];

    public int TimeCap => TimeCapSpin.Editable ? (int)TimeCapSpin.Value : 0;
    public int PointsCap => PointsCapSpin.Editable ? (int)PointsCapSpin.Value : 0;
    public int MaximumPlayers => Mathf.Min((int)MaximumPlayersSpin.Value, SelectedMap.PlayersCap);

    public int MaximumHealth => (int)MaximumHealthSpin.Value;
    public float Speed => (float)SpeedSpin.Value;
    public int GunDamage => (int)GunDamageSpin.Value;
    public int GunAmmo => (int)GunAmmoSpin.Value;
    public int MeleeDamage => (int)MeleeDamageSpin.Value;

    public MapResource SelectedMap { get; private set; }
    public Dictionary Rules
    {
        get => new()
        {
            { "Time", TimeCap },
            { "Points", PointsCap },
            { "Maximum", MaximumPlayers },
            { "Health", MaximumHealth },
            { "Speed", Speed },
            { "GunDamage", GunDamage },
            { "GunAmmo", GunAmmo },
            { "MeleeDamage", MeleeDamage },
            { "Name", SelectedMap.Name }
        };
    }

    public override void _Ready()
    {
        TimerEnabled.Toggled += (state) => TimeCapSpin.Editable = state;
        KillCapEnabled.Toggled += (state) => PointsCapSpin.Editable = state;
        StartButton.Pressed += () =>
        {
            if (SelectedMap == null) return;
            NetworkManager.Instance.HostGame(SelectedMap.MapScenePath, Rules);
        };
        BackButton.Pressed += Hide;
        foreach (var map in MapRoster)
        {
            TextureButton tile = MapTile.Instantiate() as TextureButton;
            tile.TextureNormal = map.Preview;
            tile.GetNode<Label>("MapName").Text = $"{map.Name} (max: {map.PlayersCap})";
            tile.SetMeta("MapInfo", map);
            tile.Pressed += () =>
            {
                SelectedMap = tile.GetMeta("MapInfo").As<MapResource>();
                MaximumPlayersSpin.MaxValue = SelectedMap.PlayersCap;
                StartButton.Text = $"Start {SelectedMap.Name}";
                GD.Print(SelectedMap.Name);
            };
            MapTilesBox.AddChild(tile);
        }
    }
}
