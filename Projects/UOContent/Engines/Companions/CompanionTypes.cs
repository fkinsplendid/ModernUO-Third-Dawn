namespace Server.Engines.Companions;

public enum CompanionRole
{
    Warrior,
    Bard,
    Mage
}

public enum CompanionPersonality
{
    Stoic,
    Cheerful,
    Gruff,
    Witty
}

public enum CompanionState
{
    /// <summary>Out in the world with their employer.</summary>
    Active,

    /// <summary>Employer is offline; tucked away until they log back in.</summary>
    Waiting,

    /// <summary>Back at the guild, either dismissed or unpaid. Progress is kept.</summary>
    Resting
}

/// <summary>A stat that grows linearly with level.</summary>
public readonly record struct StatCurve(int Base, double PerLevel)
{
    public int At(int level) => Base + (int)(PerLevel * (level - 1));
}

/// <summary>
/// Everything that distinguishes one companion role from another, as data.
/// </summary>
public sealed class CompanionProfile
{
    public CompanionRole Role { get; init; }
    public string Title { get; init; }
    public StatCurve Str { get; init; }
    public StatCurve Dex { get; init; }
    public StatCurve Int { get; init; }
    public int BonusHitsPerLevel { get; init; }

    /// <summary>Skills that are trained with level. Everything else stays at zero.</summary>
    public SkillName[] Skills { get; init; }
}
