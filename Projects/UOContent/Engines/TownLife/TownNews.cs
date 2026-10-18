using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.CharacterCreation;
using Server.Engines.Companions;
using Server.Mobiles;
using Server.Regions;

namespace Server.Engines.TownLife;

/// <summary>Kinds of thing that happen that townsfolk talk about.</summary>
public enum NewsKind
{
    /// <summary>A player slew something fearsome (an ogre lord, a dragon, a lich lord).</summary>
    BigKill,

    /// <summary>A player died to a monster.</summary>
    Died,

    /// <summary>A player was murdered by another.</summary>
    Murder,

    /// <summary>A player became a Grandmaster of a skill.</summary>
    Grandmaster,

    /// <summary>A new adventurer came to Britannia.</summary>
    Newcomer,

    /// <summary>One of the townsfolk was killed by a player.</summary>
    ResidentKilled,

    /// <summary>A player's hired companion fell.</summary>
    CompanionLost,

    Count
}

/// <summary>
/// Something that happened. <see cref="What"/> keeps its article ("an ogre lord", "a dragon", or a
/// boss's own name), so lines read "{who} slew {what}". <see cref="Who"/> is the player it's about (kept so townsfolk can
/// recognise them); <see cref="What"/> is the creature, skill or victim; <see cref="Where"/> is
/// the dungeon or town; <see cref="Town"/> is set when it's one of that town's own people.
/// </summary>
public sealed class NewsItem
{
    public NewsKind Kind { get; init; }

    public Mobile Who { get; init; }

    public string WhoName { get; init; }

    public string What { get; init; }

    public string Where { get; init; }

    public string Town { get; init; }

    public DateTime When { get; init; }
}

/// <summary>
/// The talk of the town: notable things that really happened on the shard, kept for a couple of
/// days (and across restarts) for townsfolk to gossip about and to recognise people by.
/// </summary>
public sealed class TownNews : GenericPersistence
{
    private const int MaxItems = 60;

    // Big enough to be news: ogre lords, dragons, liches lords and up.
    private const int NewsworthyFame = 10000;

    private static readonly List<NewsItem> _items = [];
    private static TownNews _persistence;

    public static TimeSpan Lifetime { get; private set; } = TimeSpan.FromDays(2);

    public static IReadOnlyList<NewsItem> Items => _items;

    public TownNews() : base("TownNews", 10)
    {
    }

    public static void Configure()
    {
        Lifetime = ServerConfiguration.GetOrUpdateSetting("townLife.newsLifetime", TimeSpan.FromDays(2));
        _persistence = new TownNews();
    }

    /// <summary>For tests.</summary>
    internal static void Clear() => _items.Clear();

    // ---------- Recording ----------

    public static void Add(NewsItem item)
    {
        Expire();

        if (_items.Count >= MaxItems)
        {
            _items.RemoveAt(0);
        }

        _items.Add(item);
    }

    [OnEvent(nameof(BaseCreature.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature dead)
    {
        if (!TownLifeSystem.Enabled || dead.Map == null || dead.Map == Map.Internal)
        {
            return;
        }

        var killer = PlayerBehind(dead.LastKiller);

        switch (dead)
        {
            case Resident resident when killer != null:
                {
                    Add(
                        new NewsItem
                        {
                            Kind = NewsKind.ResidentKilled, Who = killer, WhoName = killer.Name,
                            What = resident.Name, Where = PlaceName(dead), Town = resident.TownName, When = Core.Now
                        }
                    );
                    break;
                }
            case BaseCompanion companion when companion.Employer is PlayerMobile employer:
                {
                    Add(
                        new NewsItem
                        {
                            Kind = NewsKind.CompanionLost, Who = employer, WhoName = employer.Name,
                            What = companion.Name, Where = PlaceName(dead), When = Core.Now
                        }
                    );
                    break;
                }
            case { Controlled: false, Summoned: false } when killer != null && dead.Fame >= NewsworthyFame:
                {
                    Add(
                        new NewsItem
                        {
                            Kind = NewsKind.BigKill, Who = killer, WhoName = killer.Name,
                            What = dead.Name, Where = PlaceName(dead), When = Core.Now
                        }
                    );
                    break;
                }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile dead)
    {
        if (!TownLifeSystem.Enabled || dead.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        var killer = PlayerBehind(dead.LastKiller);

        if (killer != null && killer != dead)
        {
            Add(
                new NewsItem
                {
                    Kind = NewsKind.Murder, Who = dead, WhoName = dead.Name, What = killer.Name,
                    Where = PlaceName(dead), When = Core.Now
                }
            );
        }
        else if (dead.LastKiller is { } monster && killer == null)
        {
            Add(
                new NewsItem
                {
                    Kind = NewsKind.Died, Who = dead, WhoName = dead.Name,
                    What = monster.Name, Where = PlaceName(dead), When = Core.Now
                }
            );
        }
    }

    [OnEvent(nameof(CharacterCreation.CharacterCreation.CharacterCreatedEvent))]
    public static void OnCharacterCreated(CharacterCreatedEventArgs e)
    {
        // The new character is made by another handler of the same event, which may run after this one.
        if (TownLifeSystem.Enabled)
        {
            Timer.DelayCall(TimeSpan.Zero, RecordNewcomer, e);
        }
    }

    private static void RecordNewcomer(CharacterCreatedEventArgs e)
    {
        if (e.Mobile is PlayerMobile { Deleted: false } newcomer)
        {
            Add(new NewsItem { Kind = NewsKind.Newcomer, Who = newcomer, WhoName = newcomer.Name, When = Core.Now });
        }
    }

    /// <summary>Called from <see cref="PlayerMobile.OnSkillChange"/>: reaching 100 is news.</summary>
    public static void OnSkillChange(PlayerMobile player, SkillName skill, double oldBase)
    {
        if (!TownLifeSystem.Enabled || oldBase >= 100.0 || player.Skills[skill].Base < 100.0 ||
            player.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        Add(
            new NewsItem
            {
                Kind = NewsKind.Grandmaster, Who = player, WhoName = player.Name,
                What = SkillInfo.Table[(int)skill].Name.ToLowerInvariant(), When = Core.Now
            }
        );
    }

    /// <summary>The player responsible: themselves, or the master of the pet, summons or companion that did it.</summary>
    private static PlayerMobile PlayerBehind(Mobile m) =>
        m switch
        {
            PlayerMobile player                                      => player,
            BaseCompanion { Employer: PlayerMobile employer }        => employer,
            BaseCreature { ControlMaster: PlayerMobile master }      => master,
            BaseCreature { SummonMaster: PlayerMobile summoner }     => summoner,
            BaseCreature { BardProvoked: true, BardMaster: var bard } => PlayerBehind(bard),
            _                                                        => null
        };

    /// <summary>The dungeon or town it happened in, by its outermost named region; otherwise "the wilds".</summary>
    public static string PlaceName(Mobile m)
    {
        string name = null;

        for (var region = m.Region; region != null; region = region.Parent)
        {
            if (!string.IsNullOrEmpty(region.Name) && region.Name != "Moongates" &&
                region is DungeonRegion or TownRegion or GuardedRegion)
            {
                name = region.Name;
            }
        }

        return name ?? "the wilds";
    }

    // ---------- Asking ----------

    private static void Expire()
    {
        var now = Core.Now;

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (now - _items[i].When > Lifetime)
            {
                _items.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Something worth talking about in <paramref name="town"/>: anything from across the land,
    /// or that town's own losses. Recent news is likelier. Null if nothing has happened.
    /// </summary>
    public static NewsItem PickFor(string town)
    {
        Expire();

        NewsItem chosen = null;
        var total = 0;
        var now = Core.Now;

        foreach (var item in _items)
        {
            if (item.Town != null && item.Town != town)
            {
                continue;
            }

            // Fresh news is on everyone's lips; day-old news less so.
            var weight = (now - item.When).TotalHours switch
            {
                < 1  => 6,
                < 6  => 3,
                _    => 1
            };

            total += weight;

            if (Utility.Random(total) < weight)
            {
                chosen = item;
            }
        }

        return chosen;
    }

    /// <summary>The most recent news about <paramref name="player"/> of the given kind, if any.</summary>
    public static NewsItem About(Mobile player, NewsKind kind, string town = null)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];

            if (item.Who == player && item.Kind == kind && (town == null || item.Town == null || item.Town == town) &&
                Core.Now - item.When < Lifetime)
            {
                return item;
            }
        }

        return null;
    }

    // ---------- Saving ----------

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.WriteEncodedInt(_items.Count);

        foreach (var item in _items)
        {
            writer.WriteEncodedInt((int)item.Kind);
            writer.Write(item.Who);
            writer.Write(item.WhoName);
            writer.Write(item.What);
            writer.Write(item.Where);
            writer.Write(item.Town);
            writer.Write(item.When);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            _items.Add(
                new NewsItem
                {
                    Kind = (NewsKind)reader.ReadEncodedInt(),
                    Who = reader.ReadEntity<Mobile>(),
                    WhoName = reader.ReadString(),
                    What = reader.ReadString(),
                    Where = reader.ReadString(),
                    Town = reader.ReadString(),
                    When = reader.ReadDateTime()
                }
            );
        }
    }
}
