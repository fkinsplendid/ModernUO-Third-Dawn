namespace Server.Engines.TownLife;

/// <summary>What a resident does for a living, which decides where their day takes them.</summary>
public enum ResidentTrade
{
    Farmer,
    Fisher,
    Smith,
    Townsfolk,
    Shopper,
    Drunk,
    Minstrel,
    Watchman,
    Beggar,
    Courier,
    Noble,
    Lurker,
    Count
}

/// <summary>The kinds of place a town is made of.</summary>
public enum PlaceKind
{
    Street,
    Home,
    Tavern,
    Bank,
    Market,
    Smithy,
    Docks,
    Fields,
    Healer,
    Count
}

/// <summary>The parts of a Britannian day, by the game clock.</summary>
public enum DayPhase
{
    Night,     // 22:00 - 05:00
    Dawn,      // 05:00 - 08:00
    Morning,   // 08:00 - 12:00
    Midday,    // 12:00 - 14:00
    Afternoon, // 14:00 - 18:00
    Evening,   // 18:00 - 22:00
    Count
}

/// <summary>Personal manner, which colours how a resident speaks.</summary>
public enum ResidentManner
{
    Friendly,
    Gruff,
    Nervous,
    Chatty,
    Count
}
