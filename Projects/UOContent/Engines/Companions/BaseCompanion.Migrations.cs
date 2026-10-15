namespace Server.Engines.Companions;

public abstract partial class BaseCompanion
{
    // v1 remembers what a companion was wearing when they died.
    private void MigrateFrom(V0Content content)
    {
        _employer = content.Employer;
        _state = content.State;
        _personality = content.Personality;
        _level = content.Level;
        _experience = content.Experience;
        _purse = content.Purse;
        _minutesServed = content.MinutesServed;
        _kills = content.Kills;
        _quiet = content.Quiet;
        _issuedGear = content.IssuedGear;
    }

    // v2 adds the steed and the order to stay on foot.
    private void MigrateFrom(V1Content content)
    {
        _employer = content.Employer;
        _state = content.State;
        _personality = content.Personality;
        _level = content.Level;
        _experience = content.Experience;
        _purse = content.Purse;
        _minutesServed = content.MinutesServed;
        _kills = content.Kills;
        _quiet = content.Quiet;
        _issuedGear = content.IssuedGear;
        _wornAtDeath = content.WornAtDeath;
    }

    // v3 tells looted potions, which are shared, from potions given to drink.
    private void MigrateFrom(V2Content content)
    {
        _employer = content.Employer;
        _state = content.State;
        _personality = content.Personality;
        _level = content.Level;
        _experience = content.Experience;
        _purse = content.Purse;
        _minutesServed = content.MinutesServed;
        _kills = content.Kills;
        _quiet = content.Quiet;
        _issuedGear = content.IssuedGear;
        _wornAtDeath = content.WornAtDeath;
        _steed = content.Steed;
        _stayOnFoot = content.StayOnFoot;
    }
}
