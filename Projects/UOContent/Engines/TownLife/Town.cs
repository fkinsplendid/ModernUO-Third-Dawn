using System;
using System.Collections.Generic;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Server.Engines.TownLife;

/// <summary>
/// One living town: its streets, the places in it, and the people who live there. Places are
/// found rather than placed by hand: vendors mark the tavern, bank, smithy, docks, healer and
/// shops, the town's named field regions mark the farmland, and front doors are homes.
/// </summary>
public sealed class Town
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(Town));

    // Doors this close together are one building's double door: one home.
    private const int DoorPairRange = 2;

    // Fewer front doors than this and the town's doors were most likely never generated.
    private const int FewHomes = 5;

    private readonly List<TownPlace>[] _places = new List<TownPlace>[(int)PlaceKind.Count];
    private readonly List<Resident> _residents = [];
    private readonly (ResidentTrade Trade, int Count)[] _roster;
    private readonly long[] _vacantSince = new long[(int)ResidentTrade.Count];
    private Mobile _probe;

    public Town(string name, Map map, (ResidentTrade Trade, int Count)[] roster, string[] localTalk = null)
    {
        Name = name;
        Map = map;
        _roster = roster;
        LocalTalk = localTalk ?? [];

        for (var i = 0; i < _places.Length; i++)
        {
            _places[i] = [];
        }
    }

    public string Name { get; }

    /// <summary>Remarks only a local would make.</summary>
    public string[] LocalTalk { get; }

    public Map Map { get; }

    public Region Region { get; private set; }

    public StreetMap Streets { get; private set; }

    public bool IsReady => Streets?.IsReady == true && PlacesFound;

    public bool PlacesFound { get; private set; }

    public IReadOnlyList<Resident> Residents => _residents;

    /// <summary>Tick count before which nobody else in town starts talking, so voices don't pile up.</summary>
    public long NextVoice { get; set; }

    /// <summary>Tick count before which no new conversation starts, so the town isn't one long chatter.</summary>
    public long NextConversation { get; set; }

    public IReadOnlyList<TownPlace> PlacesOf(PlaceKind kind) => _places[(int)kind];

    /// <summary>The part of the day, by the town clock. Everyone in town keeps the same hours.</summary>
    public DayPhase Phase => ResidentSchedule.PhaseAt(Map, Region.GoLocation);

    public bool Has(PlaceKind kind) => kind == PlaceKind.Street || _places[(int)kind].Count > 0;

    // ---------- Building ----------

    /// <summary>Finds the region and starts flooding its streets. False if the town doesn't exist on this map.</summary>
    public bool Begin()
    {
        Region = Region.Find(Name, Map);

        if (Region == null || Region.Area.Length == 0)
        {
            logger.Warning("Town life: no region named {Name} on {Map}", Name, Map);
            return false;
        }

        int x1 = int.MaxValue, y1 = int.MaxValue, x2 = int.MinValue, y2 = int.MinValue;

        foreach (var area in Region.Area)
        {
            x1 = Math.Min(x1, area.Start.X);
            y1 = Math.Min(y1, area.Start.Y);
            x2 = Math.Max(x2, area.End.X);
            y2 = Math.Max(y2, area.End.Y);
        }

        Streets = new StreetMap(Map, Region, new Rectangle2D(x1, y1, x2 - x1 + 1, y2 - y1 + 1));

        // A throwaway walker to test steps with: the movement rules need a body.
        _probe = new Resident(ResidentTrade.Townsfolk, null);
        _probe.MoveToWorld(Region.GoLocation, Map.Internal);

        Streets.Seed(Region.GoLocation);

        // Vendors stand indoors, often behind doors the flood hasn't reached from the centre yet.
        foreach (var vendor in FindVendors())
        {
            Streets.Seed(vendor.Location);
        }

        return true;
    }

    /// <summary>One slice of the street flood; then, once the streets are known, the places.</summary>
    public void BuildStep(int budget)
    {
        if (Streets == null || IsReady)
        {
            return;
        }

        Streets.Build(_probe, budget);

        if (!Streets.IsReady)
        {
            return;
        }

        _probe?.Delete();
        _probe = null;

        FindPlaces();
        logger.Information(
            "Town life: {Name} has {Tiles} walkable tiles; {Places}",
            Name,
            Streets.WalkableTiles,
            DescribePlaces()
        );

        if (_places[(int)PlaceKind.Home].Count < FewHomes)
        {
            logger.Warning(
                "Town life: {Name} found only {Homes} front doors, so its residents share them or stay out all night.",
                Name,
                _places[(int)PlaceKind.Home].Count
            );
        }
    }

    private List<BaseVendor> FindVendors()
    {
        var vendors = new List<BaseVendor>();

        foreach (var area in Region.Area)
        {
            var bounds = new Rectangle2D(area.Start.X, area.Start.Y, area.Width, area.Height);

            foreach (var vendor in Map.GetMobilesInBounds<BaseVendor>(bounds))
            {
                if (!vendors.Contains(vendor))
                {
                    vendors.Add(vendor);
                }
            }
        }

        return vendors;
    }

    private void FindPlaces()
    {
        foreach (var list in _places)
        {
            list.Clear();
        }

        foreach (var vendor in FindVendors())
        {
            if (KindOf(vendor) is not { } kind || !Streets.TryFindNearest(vendor.Location, 3, out var spot))
            {
                continue;
            }

            _places[(int)kind].Add(new TownPlace(kind, vendor.Name ?? kind.ToString(), spot, vendor));
        }

        foreach (var region in Region.Regions)
        {
            if (region.Parent == Region && region.Map == Map && region.Name?.Contains("Field") == true &&
                region.Area.Length > 0)
            {
                var area = region.Area[0];
                var center = new Point3D(area.Start.X + area.Width / 2, area.Start.Y + area.Height / 2, 0);

                if (Streets.TryFindNearest(center, 8, out var spot))
                {
                    _places[(int)PlaceKind.Fields].Add(new TownPlace(PlaceKind.Fields, region.Name, spot));
                }
            }
        }

        FindHomes();
        PlacesFound = true;
    }

    /// <summary>
    /// Front doors: open sky on one side, a roof on the other. Doors between rooms (the castle is
    /// full of them) aren't anyone's home, nor are shop and tavern doors. Residents wait in the street
    /// outside theirs.
    /// </summary>
    private void FindHomes()
    {
        var homes = _places[(int)PlaceKind.Home];

        foreach (var area in Region.Area)
        {
            var bounds = new Rectangle2D(area.Start.X, area.Start.Y, area.Width, area.Height);

            foreach (var door in Map.GetItemsInBounds<BaseDoor>(bounds))
            {
                var location = door.Location;
                var taken = false;

                for (var i = 0; i < homes.Count && !taken; i++)
                {
                    taken = Utility.InRange(homes[i].Spot, location, DoorPairRange);
                }

                if (taken || IsShopDoor(location) || !TryFindDoorstep(location, out var spot))
                {
                    continue;
                }

                homes.Add(new TownPlace(PlaceKind.Home, "home", spot));
            }
        }

        if (homes.Count < ArchwayHomes)
        {
            FindArchwayHomes();
        }
    }

    // Below this many front doors, archways count as front doors too (Magincia has hardly a door in it).
    private const int ArchwayHomes = 10;
    private const int ArchwaySpacing = 3;

    private void FindArchwayHomes()
    {
        var homes = _places[(int)PlaceKind.Home];

        foreach (var area in Region.Area)
        {
            for (var x = area.Start.X; x < area.End.X; x++)
            {
                for (var y = area.Start.Y; y < area.End.Y; y++)
                {
                    if (!TryFindArchway(x, y, out var arch, out var spot, out _))
                    {
                        continue;
                    }

                    var taken = false;

                    for (var i = 0; i < homes.Count && !taken; i++)
                    {
                        taken = Utility.InRange(homes[i].Spot, spot, ArchwaySpacing);
                    }

                    if (!taken && !IsShopDoor(arch))
                    {
                        homes.Add(new TownPlace(PlaceKind.Home, "home", spot));
                    }
                }
            }
        }
    }

    /// <summary>
    /// A doorless entrance: a roofed gap in a wall, up to three tiles wide, with a room behind it and
    /// open sky in front (past a portico, perhaps). <paramref name="outside"/> is that street tile.
    /// </summary>
    private bool TryFindArchway(int x, int y, out Point3D arch, out Point3D outside, out Direction way)
    {
        outside = default;
        way = Direction.North;

        if (!Streets.TryGetTile(x, y, out arch) || Streets.IsOutdoors(arch))
        {
            return false;
        }

        for (var d = 0; d < 8; d += 2)
        {
            int ox = x, oy = y, ix = x, iy = y;
            Movement.Movement.Offset((Direction)d, ref ox, ref oy);
            Movement.Movement.Offset((Direction)((d + 4) & 7), ref ix, ref iy);

            // The street may lie past a portico or the eaves, as it can for a door.
            if (!Streets.TryGetTile(ox, oy, out var front) || !TryFindOpenSky(front, (Direction)d, out var street) ||
                !Streets.TryGetTile(ix, iy, out var behind) || Streets.IsOutdoors(behind) ||
                !IsWallGap(x, y, (Direction)((d + 2) & 7)))
            {
                continue;
            }

            outside = street;
            way = (Direction)d;
            return true;
        }

        return false;
    }

    // An arch is at most this wide; anything wider is the open side of a building, not an entrance.
    private const int ArchWidth = 3;

    // Walls either side, across the given way, no more than ArchWidth tiles apart.
    private bool IsWallGap(int x, int y, Direction across)
    {
        var width = 1 + WalkableRun(x, y, across) + WalkableRun(x, y, (Direction)(((int)across + 4) & 7));
        return width <= ArchWidth;
    }

    private int WalkableRun(int x, int y, Direction way)
    {
        for (var run = 0; run < ArchWidth; run++)
        {
            Movement.Movement.Offset(way, ref x, ref y);

            if (!Streets.IsWalkable(x, y))
            {
                return run;
            }
        }

        return ArchWidth;
    }

    /// <summary>
    /// The street outside a front door: open sky straight out from it, within a few steps (past a
    /// porch or the eaves), with a roof on the other side. A door between two rooms has neither.
    /// </summary>
    private bool TryFindDoorstep(Point3D door, out Point3D doorstep)
    {
        doorstep = default;
        var indoors = false;

        for (var d = 0; d < 8; d += 2)
        {
            int x = door.X, y = door.Y;
            Movement.Movement.Offset((Direction)d, ref x, ref y);

            if (!Streets.TryGetTile(x, y, out var tile))
            {
                continue;
            }

            if (doorstep == default && TryFindOpenSky(tile, (Direction)d, out var outside))
            {
                doorstep = outside;
            }
            else if (!Streets.IsOutdoors(tile))
            {
                indoors = true;
            }
        }

        return indoors && doorstep != default;
    }

    // How far past a door the eaves or a porch can reach before the open street.
    private const int PorchDepth = 3;

    private bool TryFindOpenSky(Point3D from, Direction way, out Point3D outside)
    {
        var tile = from;

        for (var step = 0; step < PorchDepth; step++)
        {
            if (Streets.IsOutdoors(tile))
            {
                outside = tile;
                return true;
            }

            int x = tile.X, y = tile.Y;
            Movement.Movement.Offset(way, ref x, ref y);

            if (!Streets.TryGetTile(x, y, out tile))
            {
                break;
            }
        }

        outside = default;
        return false;
    }

    private bool IsShopDoor(Point3D door)
    {
        for (var kind = PlaceKind.Tavern; kind < PlaceKind.Count; kind++)
        {
            if (kind == PlaceKind.Fields)
            {
                continue;
            }

            foreach (var place in _places[(int)kind])
            {
                if (place.Keeper != null && Utility.InRange(place.Keeper.Location, door, 6))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>What a vendor's presence makes a place. Guildmasters keep to their halls and mark nothing.</summary>
    public static PlaceKind? KindOf(BaseVendor vendor)
    {
        var type = vendor.GetType().Name;

        if (type.EndsWith("Guildmaster", StringComparison.Ordinal))
        {
            return null;
        }

        if (type.Contains("Healer", StringComparison.Ordinal))
        {
            return PlaceKind.Healer;
        }

        return type switch
        {
            "InnKeeper" or "TavernKeeper" or "Barkeeper" or "Waiter" or "Cook" => PlaceKind.Tavern,
            "Banker" or "GypsyBanker" or "Minter" => PlaceKind.Bank,
            "Blacksmith" or "Weaponsmith" or "Armorer" or "IronWorker" => PlaceKind.Smithy,
            "Fisherman" or "Shipwright" or "HarborMaster" => PlaceKind.Docks,
            "Farmer" => PlaceKind.Fields,
            _ => PlaceKind.Market
        };
    }

    public string DescribePlaces()
    {
        var parts = new List<string>();

        for (var kind = PlaceKind.Home; kind < PlaceKind.Count; kind++)
        {
            parts.Add($"{_places[(int)kind].Count} {kind}");
        }

        return string.Join(", ", parts);
    }

    // ---------- Choosing where to go ----------

    /// <summary>
    /// A spot for <paramref name="resident"/> to head for: their own front door for home, the place
    /// of that kind nearest their home otherwise (people have their usual tavern), sometimes another.
    /// </summary>
    public bool TryPickSpot(Resident resident, PlaceKind kind, out Point3D spot, out TownPlace place)
    {
        place = null;
        spot = default;

        if (kind == PlaceKind.Home)
        {
            spot = resident.Home;
            return spot != Point3D.Zero;
        }

        if (kind == PlaceKind.Street)
        {
            return Streets.TryFindRandom(out spot);
        }

        if (kind == PlaceKind.Bank && resident.Trade == ResidentTrade.Lurker && TryPickLurkSpot(resident, out spot))
        {
            return true;
        }

        var places = _places[(int)kind];

        if (places.Count == 0)
        {
            return false;
        }

        if (Utility.RandomDouble() < 0.7)
        {
            var best = double.MaxValue;

            foreach (var candidate in places)
            {
                var distance = resident.Home.GetDistanceToSqrt(candidate.Spot);

                if (distance < best)
                {
                    best = distance;
                    place = candidate;
                }
            }
        }
        else
        {
            place = places.RandomElement();
        }

        // A busy place (a dozen at the bank) fills up: try for a tile nobody else is heading for.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (Streets.TryFindNear(place!.Spot, place.Spread, out spot) && !IsSomeonesGoal(spot, resident))
            {
                return true;
            }
        }

        return Streets.TryFindNear(place!.Spot, place.Spread, out spot);
    }

    // Bank lurkers gather in the street in front of the main bank's door: centred this far out
    // from it, spread this far around, and never right in the doorway.
    private const int LurkDistance = 2;
    private const int LurkSpread = 4;
    private const int BankDoorSearch = 12;

    // Open-fronted banks can be deep: Moonglow's counter is 19 steps down a hall from its arch.
    private const int BankArchSearch = 20;

    private Point3D _bankDoor;
    private Point3D _lurkCentre;
    private int _lurkOutX;
    private int _lurkOutY;
    private bool _lurkCentreChecked;

    /// <summary>
    /// Somewhere in front of the main bank: the one nearest the town's centre (in Britain, the west
    /// bank). Out in the street facing its entrance, not in the doorway and not round the side.
    /// </summary>
    private bool TryPickLurkSpot(Resident resident, out Point3D spot)
    {
        spot = default;

        if (!_lurkCentreChecked)
        {
            _lurkCentreChecked = true;
            FindBankFront();
        }

        if (_lurkCentre == Point3D.Zero)
        {
            return false;
        }

        for (var attempt = 0; attempt < 16; attempt++)
        {
            if (Streets.TryFindNear(_lurkCentre, LurkSpread, out spot) && Streets.IsOutdoors(spot) &&
                IsInFrontOfBank(spot) && !IsSomeonesGoal(spot, resident))
            {
                return true;
            }
        }

        spot = _lurkCentre;
        return true;
    }

    /// <summary>
    /// The main bank's entrance: of the doors with a doorstep near its counter, a double door if
    /// there is one, then the nearest.
    /// </summary>
    private void FindBankFront()
    {
        _lurkCentre = Point3D.Zero;

        TownPlace main = null;
        var best = double.MaxValue;

        foreach (var bank in _places[(int)PlaceKind.Bank])
        {
            var distance = Region.GoLocation.GetDistanceToSqrt(bank.Spot);

            if (distance < best)
            {
                best = distance;
                main = bank;
            }
        }

        if (main == null)
        {
            return;
        }

        var bestScore = double.MaxValue;

        foreach (var door in Map.GetItemsInRange<BaseDoor>(main.Spot, BankDoorSearch))
        {
            if (!TryFindDoorstep(door.Location, out var doorstep))
            {
                continue;
            }

            var score = door.Location.GetDistanceToSqrt(main.Spot);

            if (HasNeighbouringDoor(door))
            {
                score -= 100;
            }

            if (score < bestScore)
            {
                bestScore = score;
                _bankDoor = door.Location;
                _lurkOutX = Math.Sign(doorstep.X - door.X);
                _lurkOutY = Math.Sign(doorstep.Y - door.Y);

                var x = doorstep.X + _lurkOutX * LurkDistance;
                var y = doorstep.Y + _lurkOutY * LurkDistance;

                _lurkCentre = Streets.TryFindNearestOutdoors(new Point3D(x, y, doorstep.Z), 1, out var centre) ? centre : doorstep;
            }
        }

        // No door: an archway, then the street nearest the counter.
        if (_lurkCentre == Point3D.Zero)
        {
            FindBankArchway(main.Spot);
        }

        if (_lurkCentre == Point3D.Zero && Streets.TryFindNearestOutdoors(main.Spot, BankDoorSearch, out var outside))
        {
            _lurkCentre = outside;
        }

        logger.Information(
            "Town life: {Name} bank lurkers gather at {Centre}, before the door at {Door}",
            Name,
            _lurkCentre,
            _bankDoor
        );
    }

    /// <summary>The archway nearest the counter, for banks with an open front (Trinsic, Moonglow, Magincia).</summary>
    private void FindBankArchway(Point3D counter)
    {
        var best = double.MaxValue;

        for (var x = counter.X - BankArchSearch; x <= counter.X + BankArchSearch; x++)
        {
            for (var y = counter.Y - BankArchSearch; y <= counter.Y + BankArchSearch; y++)
            {
                if (!TryFindArchway(x, y, out var arch, out var doorstep, out var way))
                {
                    continue;
                }

                var score = arch.GetDistanceToSqrt(counter);

                if (score >= best)
                {
                    continue;
                }

                best = score;
                _bankDoor = arch;
                _lurkOutX = Math.Sign(doorstep.X - arch.X);
                _lurkOutY = Math.Sign(doorstep.Y - arch.Y);

                var centre = new Point3D(doorstep.X + _lurkOutX * LurkDistance, doorstep.Y + _lurkOutY * LurkDistance, doorstep.Z);
                _lurkCentre = Streets.TryFindNearestOutdoors(centre, 1, out var found) ? found : doorstep;
            }
        }
    }

    private bool HasNeighbouringDoor(BaseDoor door)
    {
        foreach (var other in Map.GetItemsInRange<BaseDoor>(door.Location, 1))
        {
            if (other != door)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Out in front of the entrance, clear of the doorway itself.</summary>
    private bool IsInFrontOfBank(Point3D spot)
    {
        if (_lurkOutX == 0 && _lurkOutY == 0)
        {
            return true;
        }

        var dx = spot.X - _bankDoor.X;
        var dy = spot.Y - _bankDoor.Y;
        var outward = dx * _lurkOutX + dy * _lurkOutY;
        var across = Math.Abs(dx * _lurkOutY - dy * _lurkOutX);

        return outward >= 2 && !(outward <= 2 && across <= 1);
    }

    private bool IsSomeonesGoal(Point3D spot, Resident resident)
    {
        foreach (var other in _residents)
        {
            if (other != resident && other.Goal.X == spot.X && other.Goal.Y == spot.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A front door nobody else lives behind yet, if there is one.</summary>
    public Point3D PickHome()
    {
        var homes = _places[(int)PlaceKind.Home];

        if (homes.Count == 0)
        {
            return Point3D.Zero;
        }

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var home = homes.RandomElement().Spot;
            var shared = false;

            foreach (var resident in _residents)
            {
                if (resident.Home == home)
                {
                    shared = true;
                    break;
                }
            }

            if (!shared)
            {
                return home;
            }
        }

        return homes.RandomElement().Spot;
    }

    // ---------- People ----------

    public void Register(Resident resident)
    {
        if (!_residents.Contains(resident))
        {
            _residents.Add(resident);
        }
    }

    public void Unregister(Resident resident) => _residents.Remove(resident);

    /// <summary>
    /// Keeps the cast at strength. Someone who dies is mourned a while before a newcomer takes
    /// up the trade, so a killing is noticed.
    /// </summary>
    public void Populate(bool immediately = false)
    {
        if (!IsReady)
        {
            return;
        }

        var now = Core.TickCount;

        foreach (var (trade, count) in _roster)
        {
            var have = 0;

            foreach (var resident in _residents)
            {
                if (resident.Trade == trade)
                {
                    have++;
                }
            }

            if (have >= count)
            {
                _vacantSince[(int)trade] = 0;
                continue;
            }

            ref var vacant = ref _vacantSince[(int)trade];

            if (vacant == 0 && !immediately)
            {
                vacant = now;
                continue;
            }

            if (!immediately && now - vacant < TownLifeSystem.ReplacementDelayMs)
            {
                continue;
            }

            vacant = 0;

            for (var i = have; i < count; i++)
            {
                Spawn(trade);
            }
        }
    }

    private void Spawn(ResidentTrade trade)
    {
        var resident = new Resident(trade, this) { Home = PickHome() };
        Register(resident);

        // Newcomers start where their day would have them, out of sight if possible.
        resident.TakeUpRoutine();
    }

    public int CountIndoors()
    {
        var indoors = 0;

        foreach (var resident in _residents)
        {
            if (resident.Map == Map.Internal)
            {
                indoors++;
            }
        }

        return indoors;
    }

    public void LogPopulation() =>
        logger.Information(
            "Town life: {Name} has {Count} residents, {Indoors} at home ({Phase})",
            Name,
            _residents.Count,
            CountIndoors(),
            Phase
        );

    /// <summary>Removes every resident, for a GM starting over.</summary>
    public int Clear()
    {
        var count = _residents.Count;

        for (var i = _residents.Count - 1; i >= 0; i--)
        {
            _residents[i].Delete();
        }

        _residents.Clear();
        return count;
    }
}
