using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Commands;

/// <summary>
/// One-off cleanup after switching Felucca/Trammel to the Third Dawn map files.
/// Data/third-dawn-map.json lists every tile whose land or statics differ from the old map,
/// plus areas that no longer exist at all (Haven, Khaldun, the Solen hive).
/// </summary>
public static class ThirdDawnMapCleanup
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(ThirdDawnMapCleanup));

    private const int EastStripStart = 6144;
    private static readonly Point3D _safeLocation = new(1496, 1628, 10); // Britain, Sweet Dreams Inn

    public static void Configure()
    {
        CommandSystem.Register("ThirdDawnMapCleanup", AccessLevel.Administrator, Cleanup_OnCommand);
    }

    [Usage("ThirdDawnMapCleanup")]
    [Description(
        "After switching to the Third Dawn map: deletes decoration, spawners, teleporters and NPCs left on ground that " +
        "changed or no longer exists, and moves players and pets standing there to Britain. Houses are only reported."
    )]
    public static void Cleanup_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var path = Path.Combine(Core.BaseDirectory, "Data/third-dawn-map.json");

        if (!File.Exists(path))
        {
            from.SendMessage($"{path} was not found.");
            return;
        }

        var changes = MapChanges.Load(path);
        var plan = Collect(changes, LoadSpawnerGuids());

        foreach (var house in plan.Houses)
        {
            var message = $"House on changed ground: {house.GetType().Name} at {house.Location} ({house.Map}), owner {house.Owner?.Name ?? "none"}";
            from.SendMessage(0x22, message);
            logger.Warning(message);
        }

        if (plan.Items.Count + plan.Mobiles.Count + plan.Relocate.Count == 0)
        {
            from.SendMessage("Nothing left to clean up for the Third Dawn map.");
            return;
        }

        from.SendGump(new CleanupNoticeGump(plan, okay => Cleanup_Callback(from, okay, plan)));
    }

    // Spawners still defined in Data/Spawns are kept; everything else on changed ground goes.
    private static HashSet<Guid> LoadSpawnerGuids()
    {
        var guids = new HashSet<Guid>();
        var root = Path.Combine(Core.BaseDirectory, "Data/Spawns");

        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));

            foreach (var spawner in doc.RootElement.EnumerateArray())
            {
                if (spawner.TryGetProperty("guid", out var guid) && Guid.TryParse(guid.GetString(), out var id))
                {
                    guids.Add(id);
                }
            }
        }

        return guids;
    }

    private static Plan Collect(MapChanges changes, HashSet<Guid> spawnerGuids)
    {
        var plan = new Plan();

        // One-off admin sweep, same approach as [ClearAll.
        foreach (var item in World.Items.Values)
        {
            if (item.Parent != null || item.Map != Map.Felucca && item.Map != Map.Trammel)
            {
                continue;
            }

            if (item is BaseHouse house)
            {
                if (changes.IsAffected(house.Map, house.X, house.Y))
                {
                    plan.Houses.Add(house);
                }

                continue;
            }

            if (item is BaseMulti or Corpse or HouseSign || BaseHouse.FindHouseAt(item) != null)
            {
                continue;
            }

            if (item is Teleporter teleporter)
            {
                var destMap = teleporter.MapDest ?? teleporter.Map;
                var dest = teleporter.PointDest;

                if (changes.IsAffected(item.Map, item.X, item.Y) ||
                    (destMap == Map.Felucca || destMap == Map.Trammel) &&
                    (dest.X >= EastStripStart || changes.IsAffected(destMap, dest.X, dest.Y)))
                {
                    plan.Items.Add(item);
                }

                continue;
            }

            if (!changes.IsAffected(item.Map, item.X, item.Y))
            {
                continue;
            }

            if (item is BaseSpawner spawner)
            {
                if (!spawnerGuids.Contains(spawner.Guid))
                {
                    plan.Items.Add(item);
                }
            }
            else if (!item.Movable)
            {
                plan.Items.Add(item);
            }
        }

        foreach (var mob in World.Mobiles.Values)
        {
            var map = mob.Map == Map.Internal ? mob.LogoutMap : mob.Map;
            var loc = mob.Map == Map.Internal ? mob.LogoutLocation : mob.Location;

            if (map != Map.Felucca && map != Map.Trammel || loc.X < EastStripStart && !changes.IsAffected(map, loc.X, loc.Y))
            {
                continue;
            }

            if (mob.Player || mob is BaseCreature { Controlled: true } or BaseCreature { Summoned: true })
            {
                plan.Relocate.Add(mob);
            }
            else if (mob.Map != Map.Internal && BaseHouse.FindHouseAt(mob) == null)
            {
                plan.Mobiles.Add(mob);
            }
        }

        return plan;
    }

    private static void Cleanup_Callback(Mobile from, bool okay, Plan plan)
    {
        if (!okay)
        {
            from.SendMessage("You have chosen not to clean up the Third Dawn map.");
            return;
        }

        CommandLogging.WriteLine(from, $"{from.AccessLevel} {CommandLogging.Format(from)} running Third Dawn map cleanup: {plan}");

        NetState.FlushAll();

        foreach (var item in plan.Items)
        {
            item.Delete();
        }

        foreach (var mob in plan.Mobiles)
        {
            mob.Delete();
        }

        foreach (var mob in plan.Relocate)
        {
            if (mob.Map == Map.Internal)
            {
                mob.LogoutLocation = _safeLocation;
                mob.LogoutMap = Map.Trammel;
            }
            else
            {
                mob.MoveToWorld(_safeLocation, Map.Trammel);
            }
        }

        from.SendMessage($"Third Dawn map cleanup done: {plan}.");
        from.SendMessage("Now run [Decorate, [SignGen, [TelGen and [GenerateSpawners Data/Spawns/shared/trammel/Ocllo.json, then save.");
    }

    private class MapChanges
    {
        private readonly Dictionary<Map, HashSet<int>> _tiles = new();
        private readonly List<(Map Map, Rectangle2D Area)> _removedAreas = [];

        public static MapChanges Load(string path)
        {
            var changes = new MapChanges();
            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            foreach (var area in doc.RootElement.GetProperty("removedAreas").EnumerateArray())
            {
                var map = Map.Parse(area.GetProperty("map").GetString());
                var x1 = area.GetProperty("x1").GetInt32();
                var y1 = area.GetProperty("y1").GetInt32();
                var x2 = area.GetProperty("x2").GetInt32();
                var y2 = area.GetProperty("y2").GetInt32();
                changes._removedAreas.Add((map, new Rectangle2D(x1, y1, x2 - x1 + 1, y2 - y1 + 1)));
            }

            foreach (var facet in doc.RootElement.GetProperty("changedTiles").EnumerateObject())
            {
                var tiles = new HashSet<int>();

                foreach (var tile in facet.Value.EnumerateArray())
                {
                    tiles.Add(tile.GetInt32());
                }

                changes._tiles[Map.Parse(facet.Name)] = tiles;
            }

            return changes;
        }

        // Tiles are keyed as x * 4096 + y, matching the generator script.
        public bool IsAffected(Map map, int x, int y)
        {
            foreach (var (areaMap, area) in _removedAreas)
            {
                if (areaMap == map && area.Contains(new Point2D(x, y)))
                {
                    return true;
                }
            }

            return x < EastStripStart && _tiles.TryGetValue(map, out var tiles) && tiles.Contains(x * 4096 + y);
        }
    }

    private class Plan
    {
        public readonly List<Item> Items = [];
        public readonly List<Mobile> Mobiles = [];
        public readonly List<Mobile> Relocate = [];
        public readonly List<BaseHouse> Houses = [];

        public override string ToString() =>
            $"{Items.Count} items, {Mobiles.Count} NPCs deleted, {Relocate.Count} players/pets moved, {Houses.Count} houses flagged";
    }

    private class CleanupNoticeGump : StaticWarningGump<CleanupNoticeGump>
    {
        public override int Width => 380;
        public override int Height => 280;

        public override string Content { get; }

        public CleanupNoticeGump(Plan plan, Action<bool> callback) : base(callback) =>
            Content =
                $"Third Dawn map cleanup will delete {plan.Items.Count} items (decoration, spawners, teleporters) and " +
                $"{plan.Mobiles.Count} NPCs on changed ground, and move {plan.Relocate.Count} players and pets to Britain. " +
                $"{plan.Houses.Count} houses on changed ground are only reported. Continue?";
    }
}
