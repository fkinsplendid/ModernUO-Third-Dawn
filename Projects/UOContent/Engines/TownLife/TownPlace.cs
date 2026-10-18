namespace Server.Engines.TownLife;

/// <summary>
/// Somewhere in town a resident can go: a shop, the bank, a field, a front door. <see cref="Spot"/>
/// is a walkable tile there; <see cref="Keeper"/> is the vendor who runs it, if anyone does.
/// </summary>
public sealed class TownPlace
{
    public TownPlace(PlaceKind kind, string name, Point3D spot, Mobile keeper = null)
    {
        Kind = kind;
        Name = name;
        Spot = spot;
        Keeper = keeper;
    }

    public PlaceKind Kind { get; }

    public string Name { get; }

    public Point3D Spot { get; }

    public Mobile Keeper { get; }

    /// <summary>How far from the spot people spread out. Fields and docks are roomy; a front door isn't.</summary>
    public int Spread =>
        Kind switch
        {
            PlaceKind.Home   => 0,
            PlaceKind.Fields => 6,
            PlaceKind.Docks  => 4,
            PlaceKind.Street => 2,
            _                => 3
        };

    public override string ToString() => $"{Name} ({Spot.X}, {Spot.Y}, {Spot.Z})";
}
