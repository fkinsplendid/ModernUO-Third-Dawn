using System;

namespace Server.Engines.Companions;

/// <summary>
/// Tunables for the companion system. Every value is written to modernuo.json on first run
/// under the "companions." prefix so it can be adjusted without a rebuild.
/// </summary>
public static class CompanionSettings
{
    public static int HireFee { get; private set; }
    public static int MinDailyWage { get; private set; }
    public static int MaxDailyWage { get; private set; }
    public static int WageDayMinutes { get; private set; }
    public static int MaxLevel { get; private set; }
    public static double ExperienceScale { get; private set; }
    public static double GoldShare { get; private set; }
    public static int ResurrectFee { get; private set; }
    public static TimeSpan CatchUpDelay { get; private set; }
    public static TimeSpan GhostPatience { get; private set; }

    public static void Configure()
    {
        HireFee = ServerConfiguration.GetOrUpdateSetting("companions.hireFee", 1000);
        MinDailyWage = ServerConfiguration.GetOrUpdateSetting("companions.minDailyWage", 50);
        MaxDailyWage = ServerConfiguration.GetOrUpdateSetting("companions.maxDailyWage", 150);
        WageDayMinutes = Math.Max(1, ServerConfiguration.GetOrUpdateSetting("companions.wageDayMinutes", 30));
        MaxLevel = Math.Max(2, ServerConfiguration.GetOrUpdateSetting("companions.maxLevel", 30));
        ExperienceScale = ServerConfiguration.GetOrUpdateSetting("companions.experienceScale", 1.0);
        GoldShare = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("companions.goldShare", 0.1), 0.0, 1.0);
        ResurrectFee = ServerConfiguration.GetOrUpdateSetting("companions.resurrectFee", 500);
        CatchUpDelay = ServerConfiguration.GetOrUpdateSetting("companions.catchUpDelay", TimeSpan.FromSeconds(20));
        GhostPatience = ServerConfiguration.GetOrUpdateSetting("companions.ghostPatience", TimeSpan.FromMinutes(1));
    }

    public static int DailyWageFor(int level) =>
        MinDailyWage + (MaxDailyWage - MinDailyWage) * (Math.Clamp(level, 1, MaxLevel) - 1) / (MaxLevel - 1);

    /// <summary>Experience needed to advance from <paramref name="level"/> to the next.</summary>
    public static int ExperienceToAdvance(int level) => 100 * level * level;

    /// <summary>Highest a trained skill may reach at a level: 62 at level 1, 100 from level 20.</summary>
    public static double SkillCapFor(int level) => Math.Min(100.0, 60.0 + level * 2.0);

    /// <summary>
    /// Level-ups guarantee at least this much in every trained skill: a capable 52 on hire,
    /// grandmaster by level 25. Use in the field pushes skills above the floor, up to the cap.
    /// </summary>
    public static double SkillFloorFor(int level) => Math.Min(SkillCapFor(level), 50.0 + level * 2.0);
}
