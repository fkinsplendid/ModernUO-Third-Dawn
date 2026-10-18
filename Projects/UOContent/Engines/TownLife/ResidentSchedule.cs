using System;
using Server.Items;

namespace Server.Engines.TownLife;

/// <summary>A place someone might go at a given time of day, and how likely they are to choose it.</summary>
public readonly record struct ScheduleChoice(PlaceKind Kind, int Weight);

/// <summary>
/// Where each trade spends each part of the day. At night nearly everyone is at home, so the
/// streets thin out to the drunk, the night watch, the beggar and the odd minstrel.
/// </summary>
public static class ResidentSchedule
{
    private static readonly ScheduleChoice[][][] _table = new ScheduleChoice[(int)ResidentTrade.Count][][];

    static ResidentSchedule()
    {
        Day(ResidentTrade.Farmer,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Fields, 1)],
            morning: [new(PlaceKind.Fields, 1)],
            midday: [new(PlaceKind.Tavern, 1), new(PlaceKind.Fields, 1)],
            afternoon: [new(PlaceKind.Fields, 1)],
            evening: [new(PlaceKind.Tavern, 2), new(PlaceKind.Home, 3)]
        );
        Day(ResidentTrade.Fisher,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Docks, 1)],
            morning: [new(PlaceKind.Docks, 1)],
            midday: [new(PlaceKind.Market, 1), new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Docks, 1)],
            evening: [new(PlaceKind.Tavern, 3), new(PlaceKind.Home, 2)]
        );
        Day(ResidentTrade.Smith,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Smithy, 1)],
            morning: [new(PlaceKind.Smithy, 1)],
            midday: [new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Smithy, 1)],
            evening: [new(PlaceKind.Tavern, 1), new(PlaceKind.Home, 1)]
        );
        Day(ResidentTrade.Townsfolk,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Home, 7), new(PlaceKind.Market, 3)],
            morning: [new(PlaceKind.Market, 3), new(PlaceKind.Street, 2)],
            midday: [new(PlaceKind.Market, 2), new(PlaceKind.Healer, 1), new(PlaceKind.Bank, 1)],
            afternoon: [new(PlaceKind.Street, 2), new(PlaceKind.Market, 2)],
            evening: [new(PlaceKind.Tavern, 3), new(PlaceKind.Home, 7)]
        );
        Day(ResidentTrade.Shopper,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Home, 1)],
            morning: [new(PlaceKind.Market, 2), new(PlaceKind.Bank, 1)],
            midday: [new(PlaceKind.Market, 1)],
            afternoon: [new(PlaceKind.Market, 2), new(PlaceKind.Healer, 1), new(PlaceKind.Bank, 1)],
            evening: [new(PlaceKind.Home, 1)]
        );
        Day(ResidentTrade.Drunk,
            night: [new(PlaceKind.Tavern, 1), new(PlaceKind.Street, 1)],
            dawn: [new(PlaceKind.Street, 1)],
            morning: [new(PlaceKind.Home, 1)],
            midday: [new(PlaceKind.Home, 1), new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Tavern, 1)],
            evening: [new(PlaceKind.Tavern, 1)]
        );
        Day(ResidentTrade.Minstrel,
            night: [new(PlaceKind.Tavern, 1), new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Home, 1)],
            morning: [new(PlaceKind.Market, 1), new(PlaceKind.Street, 1)],
            midday: [new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Market, 1), new(PlaceKind.Street, 1)],
            evening: [new(PlaceKind.Tavern, 1)]
        );
        Day(ResidentTrade.Watchman,
            night: [new(PlaceKind.Street, 1)],
            dawn: [new(PlaceKind.Home, 1)],
            morning: [new(PlaceKind.Home, 1)],
            midday: [new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Street, 1)],
            evening: [new(PlaceKind.Street, 2), new(PlaceKind.Tavern, 1)]
        );
        Day(ResidentTrade.Beggar,
            night: [new(PlaceKind.Street, 1)],
            dawn: [new(PlaceKind.Street, 1)],
            morning: [new(PlaceKind.Bank, 1)],
            midday: [new(PlaceKind.Tavern, 1), new(PlaceKind.Market, 1)],
            afternoon: [new(PlaceKind.Bank, 1), new(PlaceKind.Market, 1)],
            evening: [new(PlaceKind.Street, 1)]
        );
        Day(ResidentTrade.Courier,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Bank, 1)],
            morning: [new(PlaceKind.Bank, 1), new(PlaceKind.Market, 1), new(PlaceKind.Smithy, 1), new(PlaceKind.Healer, 1)],
            midday: [new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Market, 1), new(PlaceKind.Docks, 1), new(PlaceKind.Smithy, 1), new(PlaceKind.Bank, 1)],
            evening: [new(PlaceKind.Tavern, 1), new(PlaceKind.Home, 1)]
        );
        Day(ResidentTrade.Noble,
            night: [new(PlaceKind.Home, 1)],
            dawn: [new(PlaceKind.Home, 1)],
            morning: [new(PlaceKind.Bank, 1)],
            midday: [new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Market, 2), new(PlaceKind.Healer, 1)],
            evening: [new(PlaceKind.Tavern, 1), new(PlaceKind.Home, 1)]
        );
        // The bank is never empty, not even at night.
        Day(ResidentTrade.Lurker,
            night: [new(PlaceKind.Bank, 1), new(PlaceKind.Home, 2)],
            dawn: [new(PlaceKind.Bank, 1), new(PlaceKind.Home, 1)],
            morning: [new(PlaceKind.Bank, 4), new(PlaceKind.Market, 1)],
            midday: [new(PlaceKind.Bank, 3), new(PlaceKind.Tavern, 1)],
            afternoon: [new(PlaceKind.Bank, 4), new(PlaceKind.Market, 1)],
            evening: [new(PlaceKind.Bank, 3), new(PlaceKind.Tavern, 2), new(PlaceKind.Home, 1)]
        );
    }

    private static void Day(
        ResidentTrade trade, ScheduleChoice[] night, ScheduleChoice[] dawn, ScheduleChoice[] morning,
        ScheduleChoice[] midday, ScheduleChoice[] afternoon, ScheduleChoice[] evening
    ) => _table[(int)trade] = [night, dawn, morning, midday, afternoon, evening];

    public static DayPhase PhaseAt(int hour) =>
        hour switch
        {
            >= 22 or < 5 => DayPhase.Night,
            < 8          => DayPhase.Dawn,
            < 12         => DayPhase.Morning,
            < 14         => DayPhase.Midday,
            < 18         => DayPhase.Afternoon,
            _            => DayPhase.Evening
        };

    public static DayPhase PhaseAt(Map map, Point3D location)
    {
        Clock.GetTime(map, location.X, location.Y, out var hours, out int _);
        return PhaseAt(hours);
    }

    public static ReadOnlySpan<ScheduleChoice> ChoicesFor(ResidentTrade trade, DayPhase phase) =>
        _table[(int)trade][(int)phase];

    /// <summary>
    /// Picks where to go next, skipping kinds of place the town doesn't have. A street is always
    /// there to fall back on.
    /// </summary>
    public static PlaceKind Choose(ResidentTrade trade, DayPhase phase, Func<PlaceKind, bool> exists)
    {
        var choices = ChoicesFor(trade, phase);
        var total = 0;

        for (var i = 0; i < choices.Length; i++)
        {
            if (exists(choices[i].Kind))
            {
                total += choices[i].Weight;
            }
        }

        if (total == 0)
        {
            return PlaceKind.Street;
        }

        var roll = Utility.Random(total);

        for (var i = 0; i < choices.Length; i++)
        {
            if (!exists(choices[i].Kind))
            {
                continue;
            }

            roll -= choices[i].Weight;

            if (roll < 0)
            {
                return choices[i].Kind;
            }
        }

        return PlaceKind.Street;
    }

    /// <summary>How many real minutes someone of this trade stays put before finding somewhere else to be.</summary>
    public static (int Min, int Max) DwellMinutes(ResidentTrade trade) =>
        trade switch
        {
            ResidentTrade.Courier  => (1, 3),
            ResidentTrade.Watchman => (2, 4),
            ResidentTrade.Townsfolk or ResidentTrade.Shopper or ResidentTrade.Minstrel => (3, 8),
            ResidentTrade.Beggar or ResidentTrade.Drunk or ResidentTrade.Lurker => (4, 10),
            _ => (6, 15)
        };
}
