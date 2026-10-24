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
/// A skill a role trains with level. <paramref name="Bonus"/> lifts both the guaranteed floor and the
/// cap above the normal curve, for the skills the role is known for.
/// </summary>
public readonly record struct TrainedSkill(SkillName Name, double Bonus = 0)
{
    public static implicit operator TrainedSkill(SkillName name) => new(name);
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
    public TrainedSkill[] Skills { get; init; }
}

/// <summary>How a companion feels about their employer, from their regard for them.</summary>
public enum CompanionMood
{
    Bitter,
    Unhappy,
    Content,
    Loyal,
    Devoted
}

/// <summary>
/// The one thing a companion dreads. Most are kinds of foe; the dark is dread of dungeons. A fear
/// faced often enough is conquered for good.
/// </summary>
public enum CompanionFear
{
    None,
    Spiders,
    Undead,
    Snakes,
    Rats,
    Daemons,
    Dragons,
    Ooze,
    Giants,
    TheDark
}

/// <summary>What a companion loves. Getting it now and then lifts their spirits.</summary>
public enum CompanionFondness
{
    None,
    Music,
    OpenRoad,
    Treasure,
    Glory,
    Home
}

/// <summary>Things a companion may bring up with their employer, wanting an answer.</summary>
public enum CompanionIssue
{
    None,
    Weary,
    WagesShort,
    EmployerReckless,
    LeftDead,
    WantsMount,
    FacingFear,
    MissesHome,
    Notice
}
