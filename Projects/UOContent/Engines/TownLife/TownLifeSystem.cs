using System;
using System.Collections.Generic;
using Server.Commands;

namespace Server.Engines.TownLife;

/// <summary>
/// Keeps the towns alive. At start-up each town floods its streets (a slice per tick, so the
/// server isn't held up), finds its places and fills out its cast. After that, every few seconds
/// each resident's day moves on: the AI walks them while players are near; out of everyone's
/// sight this moves them, since the AI sleeps then.
///
///   [TownLife                  status of every town
///   [TownLife places           where each town's places are
///   [TownLife populate         fill any empty trades now, without the usual wait
///   [TownLife clear            remove every resident (they come back on populate)
/// </summary>
public static class TownLifeSystem
{
    // About 20,000 movement checks a tick: tens of milliseconds, for a few seconds at start-up.
    private const int BuildBudgetPerTick = 2500;
    private const int WatchRange = 20;

    private static readonly TimeSpan BuildInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan RoutineInterval = TimeSpan.FromSeconds(10);

    private static readonly Dictionary<string, Town> _towns = new(StringComparer.OrdinalIgnoreCase);
    private static TimerExecutionToken _buildTimer;
    private static TimerExecutionToken _routineTimer;

    public static bool Enabled { get; private set; } = true;

    /// <summary>How long a dead resident's trade stands empty before someone new takes it up.</summary>
    public static long ReplacementDelayMs { get; private set; }

    public static IEnumerable<Town> Towns => _towns.Values;

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("townLife.enabled", true);
        ReplacementDelayMs = (long)ServerConfiguration
            .GetOrUpdateSetting("townLife.replacementDelay", TimeSpan.FromMinutes(30))
            .TotalMilliseconds;

        // Third Dawn players live on Trammel. Britain was the pilot.
        AddTown(
            new Town(
                "Britain",
                Map.Trammel,
                [
                    (ResidentTrade.Farmer, 2),
                    (ResidentTrade.Fisher, 2),
                    (ResidentTrade.Smith, 1),
                    (ResidentTrade.Townsfolk, 3),
                    (ResidentTrade.Shopper, 2),
                    (ResidentTrade.Drunk, 1),
                    (ResidentTrade.Minstrel, 1),
                    (ResidentTrade.Watchman, 1),
                    (ResidentTrade.Beggar, 1),
                    (ResidentTrade.Courier, 1),
                    (ResidentTrade.Noble, 1),
                    (ResidentTrade.Lurker, 12)
                ],
                [
                    "The castle guard changed again. New faces every week.",
                    "Lord British rode out this morning. Or someone in a very good cloak did.",
                    "Britain's the heart of the realm. Noisy heart, mind.",
                    "Mind the crowds by the bank."
                ]
            )
        );

        // A trading city of canals and bridges; no farmland.
        AddTown(
            new Town(
                "Vesper",
                Map.Trammel,
                [
                    (ResidentTrade.Fisher, 2),
                    (ResidentTrade.Smith, 1),
                    (ResidentTrade.Townsfolk, 3),
                    (ResidentTrade.Shopper, 2),
                    (ResidentTrade.Drunk, 1),
                    (ResidentTrade.Minstrel, 1),
                    (ResidentTrade.Watchman, 1),
                    (ResidentTrade.Beggar, 1),
                    (ResidentTrade.Courier, 1),
                    (ResidentTrade.Noble, 1),
                    (ResidentTrade.Lurker, 12)
                ],
                [
                    "Mind the canal. It's deeper than it looks.",
                    "Another ore cart down from Minoc. The bridges groan under them.",
                    "Vesper's built on water and gold, they say. Mostly water.",
                    "Count the bridges in Vesper and you'll lose count. Everyone does.",
                    "Ships from Moonglow come in on the morning tide.",
                    "Orcs troubling Cove again, I hear."
                ]
            )
        );

        // A quiet island town. Small, so a smaller cast.
        AddTown(
            new Town(
                "Ocllo",
                Map.Trammel,
                [
                    (ResidentTrade.Fisher, 2),
                    (ResidentTrade.Townsfolk, 2),
                    (ResidentTrade.Shopper, 1),
                    (ResidentTrade.Drunk, 1),
                    (ResidentTrade.Watchman, 1),
                    (ResidentTrade.Beggar, 1),
                    (ResidentTrade.Courier, 1),
                    (ResidentTrade.Lurker, 6)
                ],
                [
                    "Nothing ever happens in Ocllo. I like it that way.",
                    "Island life. You get used to the quiet. Then you miss the noise.",
                    "Pirates out by Buccaneer's Den, they say. Close enough to worry.",
                    "Magincia's just across the water. You can smell the money from here.",
                    "Sea's rough today. Mind the docks.",
                    "Boat from the mainland's late again."
                ]
            )
        );

        CommandSystem.Register("TownLife", AccessLevel.GameMaster, TownLife_OnCommand);
    }

    public static void Initialize()
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var town in _towns.Values)
        {
            town.Begin();
        }

        Timer.StartTimer(BuildInterval, BuildInterval, BuildTowns, out _buildTimer);
    }

    public static void AddTown(Town town) => _towns[town.Name] = town;

    public static Town FindTown(string name) => name != null && _towns.TryGetValue(name, out var town) ? town : null;

    private static void BuildTowns()
    {
        var building = false;

        foreach (var town in _towns.Values)
        {
            if (town.Streets == null || town.IsReady)
            {
                continue;
            }

            town.BuildStep(BuildBudgetPerTick);

            if (!town.IsReady)
            {
                building = true;
                continue;
            }

            // Residents loaded from the save pick up their day where it now stands.
            var residents = town.Residents;

            for (var i = residents.Count - 1; i >= 0; i--)
            {
                residents[i].TakeUpRoutine();
            }

            town.Populate(true);
            town.LogPopulation();
        }

        if (!building)
        {
            _buildTimer.Cancel();
            Timer.StartTimer(RoutineInterval, RoutineInterval, MoveDaysOn, out _routineTimer);
        }
    }

    private static void MoveDaysOn()
    {
        foreach (var town in _towns.Values)
        {
            if (!town.IsReady)
            {
                continue;
            }

            town.Populate();

            var residents = town.Residents;

            for (var i = residents.Count - 1; i >= 0; i--)
            {
                var resident = residents[i];
                resident.CheckRoutine(IsWatched(resident));
            }
        }
    }

    /// <summary>Anyone who could see them where they are, or where they're going.</summary>
    public static bool IsWatched(Resident resident)
    {
        var map = resident.Town?.Map;

        if (map == null)
        {
            return false;
        }

        if (resident.Map == map && AnyoneNear(map, resident.Location))
        {
            return true;
        }

        return resident.Goal != Point3D.Zero && AnyoneNear(map, resident.Goal);
    }

    private static bool AnyoneNear(Map map, Point3D p)
    {
        foreach (var _ in map.GetClientsInRange(p, WatchRange))
        {
            return true;
        }

        return false;
    }

    [Usage("TownLife [who|places|populate|clear]")]
    [Description("Living towns: status, where each resident is, the places each town found, fill empty trades now, or remove every resident.")]
    private static void TownLife_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        if (!Enabled)
        {
            from.SendMessage("Town life is disabled (townLife.enabled).");
            return;
        }

        foreach (var town in _towns.Values)
        {
            switch (verb)
            {
                case "places":
                    {
                        for (var kind = PlaceKind.Home; kind < PlaceKind.Count; kind++)
                        {
                            foreach (var place in town.PlacesOf(kind))
                            {
                                from.SendMessage($"{town.Name} {kind}: {place}");
                            }
                        }

                        break;
                    }
                case "who":
                    {
                        foreach (var resident in town.Residents)
                        {
                            var where = resident.Map == Map.Internal ? "at home" : $"{resident.Location}";
                            var doing = resident.IsTravelling ? $"walking to {resident.GoalKind}" : $"{resident.GoalKind}";

                            from.SendMessage($"{resident.Name} {resident.Title}: {where}, {doing}.");
                        }

                        break;
                    }
                case "populate":
                    {
                        town.Populate(true);
                        from.SendMessage($"{town.Name}: {town.Residents.Count} residents.");
                        break;
                    }
                case "clear":
                    {
                        from.SendMessage($"{town.Name}: removed {town.Clear()} residents.");
                        break;
                    }
                default:
                    {
                        var state = town.IsReady ? "ready" : town.Streets == null ? "not started" : "mapping streets";
                        var phase = town.Region != null ? town.Phase.ToString() : "?";

                        from.SendMessage($"{town.Name} ({town.Map}): {state}, {town.Streets?.WalkableTiles ?? 0} tiles, {phase}.");
                        from.SendMessage($"  Places: {town.DescribePlaces()}");

                        if (town.IsReady && town.PlacesOf(PlaceKind.Home).Count < 5)
                        {
                            from.SendMessage("  Hardly any front doors found: residents share them or stay out all night.");
                        }

                        from.SendMessage($"  Residents: {town.Residents.Count} ({town.CountIndoors()} at home).");
                        break;
                    }
            }
        }
    }
}
