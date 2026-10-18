using Server.Engines.Companions;
using Server.Items;
using Server.Mobiles;
using Server.Regions;

namespace Server.Engines.TownLife;

/// <summary>
/// Only runs while a player is near (the engine sleeps it otherwise). Walks the route a short
/// leg at a time, works at their place once there (the smith hammers, the fisher casts), talks
/// now and then, greets passers-by, and runs for it shouting for the guards if attacked.
/// Neighbours stop to chat, trade a word in passing, or have a word with the vendor.
/// </summary>
public sealed partial class ResidentAI : BaseAI
{
    private const int WatchRange = 14;
    private const int TalkRange = 12;
    private const int GreetRange = 3;
    private const int StuckTicks = 12;
    private const int FleeMemoryMs = 10000;
    private const int ChatRange = 4;
    private const int PassingRange = 2;
    private const int KeeperChatRange = 6;
    private const double KeeperChance = 0.35;
    private const double CompanionChance = 0.5;

    private PathFollower _leg;
    private Point3D _legGoal;
    private Point3D _lastLocation;
    private int _stuck;
    private bool _settled;
    private bool _fleeing;
    private long _nextActivity;
    private long _nextSpeech;
    private long _nextGreetCheck;
    private long _nextPardon;
    private Mobile _lastGreeted;
    private long _lastGreetedAt;
    private long _nextChatCheck;
    private long _nextChat = Core.TickCount + Utility.RandomMinMax(20000, 90000);

    public ResidentAI(Resident resident) : base(resident) => Resident = resident;

    public Resident Resident { get; }

    public bool IsFleeing => _fleeing;

    /// <summary>Free to be drawn into a conversation.</summary>
    public bool ReadyToChat => !_fleeing && Core.TickCount - _nextChat >= 0 && !Conversation.IsTalking(Mobile);

    public void RestFromChat() => _nextChat = Core.TickCount + Utility.RandomMinMax(90000, 240000);

    public override bool Think()
    {
        if (Mobile.Deleted || Mobile.Map == null || Mobile.Map == Map.Internal)
        {
            return false;
        }

        if (TryFlee())
        {
            return true;
        }

        Resident.CheckRoutine(true);

        // Gone indoors.
        if (Mobile.Map == Map.Internal)
        {
            return true;
        }

        if (TryStepAway())
        {
            return true;
        }

        LookAround();

        // Deep in conversation: stand and listen.
        if (Conversation.Of(Mobile) is { Holds: true } talk)
        {
            Mobile.Direction = Mobile.GetDirectionTo(talk.PartnerOf(Mobile));
            return true;
        }

        if (Resident.TryGetWaypoint(out var waypoint))
        {
            _settled = false;
            Walk(waypoint);
        }
        else if (Resident.HasArrived)
        {
            AtPlace();
        }

        CheckGreeting();
        TryStartConversation();
        return true;
    }

    // ---------- Walking ----------

    private void Walk(Point3D waypoint)
    {
        if (Mobile.InRange(waypoint, 1))
        {
            ReachedLeg();
            return;
        }

        if (_leg == null || _legGoal != waypoint)
        {
            _leg = new PathFollower(Mobile, waypoint) { Mover = DoMoveImpl };
            _legGoal = waypoint;
            _stuck = 0;
        }

        if (_leg.Follow(false, 1))
        {
            ReachedLeg();
            return;
        }

        if (Mobile.Location == _lastLocation)
        {
            if (++_stuck == StuckTicks / 2)
            {
                ExcusePlayerInTheWay(waypoint);
            }
            else if (_stuck >= StuckTicks)
            {
                _stuck = 0;
                _leg = null;
                Resident.Reroute();
            }
        }
        else
        {
            _stuck = 0;
        }

        _lastLocation = Mobile.Location;
    }

    private void ReachedLeg()
    {
        _leg = null;
        _stuck = 0;
        Resident.ReachedWaypoint();
    }

    private void ExcusePlayerInTheWay(Point3D toward)
    {
        if (Core.TickCount - _nextPardon < 0)
        {
            return;
        }

        var direction = Mobile.GetDirectionTo(toward);
        int x = Mobile.X, y = Mobile.Y;
        Movement.Movement.Offset(direction, ref x, ref y);

        foreach (var m in Mobile.Map.GetMobilesAt(new Point2D(x, y)))
        {
            if (m is PlayerMobile { Alive: true, Hidden: false })
            {
                Mobile.Direction = direction;
                Mobile.Say(ResidentSpeech.Pardon(Resident));
                _nextPardon = Core.TickCount + 20000;
                return;
            }
        }
    }

    // ---------- At their place ----------

    private void AtPlace()
    {
        var now = Core.TickCount;

        if (!_settled)
        {
            _settled = true;
            _nextActivity = now + Utility.RandomMinMax(1000, 4000);

            var keeper = Resident.GoalPlace?.Keeper;
            Mobile.Direction = keeper?.Deleted == false && Mobile.InRange(keeper, 8)
                ? Mobile.GetDirectionTo(keeper)
                : (Direction)Utility.Random(8);
        }

        if (now - _nextActivity >= 0 && IsPlayerNear(WatchRange))
        {
            _nextActivity = now + Utility.RandomMinMax(6000, 14000);
            DoActivity();
        }

        var town = Resident.Town;

        if (town != null && now - _nextSpeech >= 0 && now - town.NextVoice >= 0 && !Conversation.IsTalking(Mobile) &&
            IsPlayerNear(TalkRange))
        {
            var line = ResidentSpeech.Idle(Resident);

            if (line != null)
            {
                Mobile.Say(line);
                town.NextVoice = now + 5000;
            }

            _nextSpeech = now + Utility.RandomMinMax(40000, 120000);
        }
    }

    /// <summary>The visible business of being somewhere: hammering, casting a line, drinking.</summary>
    private void DoActivity()
    {
        var trade = Resident.Trade;

        switch (Resident.GoalKind)
        {
            case PlaceKind.Fields when trade == ResidentTrade.Farmer:
                {
                    Mobile.Animate(Utility.RandomBool() ? 11 : 12, 5, 1, true, false, 0);
                    Mobile.PlaySound(Utility.RandomBool() ? 0x125 : 0x126);
                    break;
                }
            case PlaceKind.Docks when trade == ResidentTrade.Fisher:
                {
                    Mobile.Animate(12, 5, 1, true, false, 0);
                    Timer.StartTimer(System.TimeSpan.FromSeconds(1.5), Splash);
                    break;
                }
            case PlaceKind.Smithy when trade == ResidentTrade.Smith:
                {
                    Mobile.Animate(9, 5, 1, true, false, 0);
                    Mobile.PlaySound(0x2A);
                    break;
                }
            case PlaceKind.Tavern:
                {
                    if (Utility.RandomDouble() < 0.5)
                    {
                        Mobile.Animate(34, 5, 1, true, false, 0);
                        Mobile.PlaySound(Utility.RandomList(0x30, 0x31));
                    }
                    else if (trade == ResidentTrade.Drunk)
                    {
                        Mobile.Emote("*hic*");
                    }

                    break;
                }
            case PlaceKind.Bank when trade == ResidentTrade.Lurker && Utility.RandomDouble() < 0.4:
                {
                    Mobile.Emote(
                        Utility.RandomList(
                            "*admires a ring*",
                            "*checks the bank box again*",
                            "*flips a coin*",
                            "*yawns*",
                            "*eyes the door*",
                            "*shows off a new cloak*",
                            "*counts coins*"
                        )
                    );
                    break;
                }
            case PlaceKind.Bank when trade != ResidentTrade.Beggar && Utility.RandomDouble() < 0.3:
                {
                    Mobile.Emote("*counts coins*");
                    Mobile.PlaySound(0x2E6);
                    break;
                }
            case PlaceKind.Market when Utility.RandomDouble() < 0.3:
                {
                    Mobile.Emote(Utility.RandomBool() ? "*looks over the wares*" : "*haggles*");
                    break;
                }
            default:
                {
                    if (trade == ResidentTrade.Minstrel && Mobile.Backpack?.FindItemByType<BaseInstrument>() is { } instrument)
                    {
                        instrument.PlayInstrumentWell(Mobile);
                    }
                    else if (Utility.RandomDouble() < 0.3)
                    {
                        // Looks about.
                        Mobile.Direction = (Direction)Utility.Random(8);
                    }

                    break;
                }
        }
    }

    private void Splash()
    {
        if (!Mobile.Deleted && Mobile.Map != Map.Internal)
        {
            Mobile.PlaySound(0x364);
        }
    }

    // ---------- Conversation ----------

    /// <summary>
    /// Now and then, with someone near enough to overhear, strikes up a conversation: a word in
    /// passing while walking, or a proper chat once settled, either with a neighbour or with the
    /// vendor whose place it is.
    /// </summary>
    private void TryStartConversation()
    {
        var now = Core.TickCount;
        var town = Resident.Town;

        if (now - _nextChatCheck < 0 || town == null)
        {
            return;
        }

        _nextChatCheck = now + Utility.RandomMinMax(4000, 7000);

        if (!ReadyToChat || now - town.NextConversation < 0 || !IsPlayerNear(TalkRange))
        {
            return;
        }

        var passing = Resident.IsTravelling;

        if (!passing && !Resident.HasArrived)
        {
            return;
        }

        var talk = !passing && Utility.RandomDouble() < KeeperChance ? TryTalkToKeeper(now) : null;
        talk ??= Utility.RandomDouble() < CompanionChance ? TryTalkToCompanion(now) : null;
        talk ??= TryTalkToNeighbour(passing, town);

        if (talk == null)
        {
            return;
        }

        RestFromChat();
        town.NextConversation = now + 12000;
        town.NextVoice = now + talk.LineCount * 3500;
    }

    private Conversation TryTalkToKeeper(long now)
    {
        var place = Resident.GoalPlace;
        var keeper = place?.Keeper;

        if (keeper?.Deleted != false || !keeper.Alive || !Mobile.InRange(keeper, KeeperChatRange) ||
            !Conversation.IsFree(keeper, now))
        {
            return null;
        }

        var dialogue = ResidentDialogues.Choose(Resident.Trade, Listener.Keeper, place.Kind, Resident.Phase, false, false);
        return dialogue == null ? null : Conversation.Start(Mobile, keeper, dialogue, null);
    }

    /// <summary>A word with someone's hired companion as they pass. Not if told to keep quiet, or busy fighting.</summary>
    private Conversation TryTalkToCompanion(long now)
    {
        foreach (var companion in Mobile.Map.GetMobilesInRange<BaseCompanion>(Mobile.Location, ChatRange))
        {
            if (!CompanionParty.IsFighting(companion) || companion.Quiet || companion.Combatant != null ||
                !Conversation.IsFree(companion, now) || !Mobile.InLOS(companion))
            {
                continue;
            }

            var dialogue = ResidentDialogues.Choose(
                Resident.Trade,
                Listener.Hireling(companion.Role),
                Resident.GoalKind,
                Resident.Phase,
                false,
                false
            );

            return dialogue == null ? null : Conversation.Start(Mobile, companion, dialogue, null);
        }

        return null;
    }

    private Conversation TryTalkToNeighbour(bool passing, Town town)
    {
        foreach (var other in Mobile.Map.GetMobilesInRange<Resident>(Mobile.Location, passing ? PassingRange : ChatRange))
        {
            if (other == Mobile || !other.Alive || other.AIObject is not ResidentAI { ReadyToChat: true } otherAI ||
                passing != other.IsTravelling || !Mobile.InLOS(other))
            {
                continue;
            }

            var gossip = GossipAbout(town, other);

            // Half the time, if anything has happened, that's what they talk about. Never in passing.
            var news = !passing && Utility.RandomBool() ? TownNews.PickFor(town.Name) : null;

            // Either might open; try both ways round.
            var (opener, listener) = Utility.RandomBool() ? (Resident, other) : (other, Resident);

            if ((Choose(opener, listener, passing, gossip, news) ?? Choose(listener, opener, passing, gossip, news)) is not
                { } choice)
            {
                continue;
            }

            var talk = Conversation.Start(choice.Opener, choice.Listener, choice.Dialogue, gossip, news);

            if (talk != null)
            {
                otherAI.RestFromChat();
            }

            return talk;
        }

        return null;
    }

    private (Dialogue Dialogue, Resident Opener, Resident Listener)? Choose(
        Resident opener, Resident listener, bool passing, string gossip, NewsItem news
    )
    {
        var place = passing ? PlaceKind.Street : opener.GoalKind;
        var dialogue = ResidentDialogues.Choose(
            opener.Trade,
            Listener.Neighbour(listener.Trade),
            place,
            opener.Phase,
            passing,
            gossip != null,
            news?.Kind,
            opener.Town?.Name
        );
        return dialogue == null ? null : (dialogue, opener, listener);
    }

    /// <summary>Someone else in town to talk about.</summary>
    private string GossipAbout(Town town, Mobile other)
    {
        var residents = town.Residents;

        if (residents.Count < 3)
        {
            return null;
        }

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var subject = residents[Utility.Random(residents.Count)];

            if (subject != Mobile && subject != other)
            {
                return subject.Name;
            }
        }

        return null;
    }

    // ---------- Passers-by ----------

    private void CheckGreeting()
    {
        var now = Core.TickCount;

        if (now - _nextGreetCheck < 0)
        {
            return;
        }

        _nextGreetCheck = now + 2000;

        foreach (var player in Mobile.Map.GetMobilesInRange<PlayerMobile>(Mobile.Location, GreetRange))
        {
            if (player.Hidden || player == _lastGreeted && now - _lastGreetedAt < 600000)
            {
                continue;
            }

            _lastGreeted = player;
            _lastGreetedAt = now;

            if (Utility.RandomDouble() < 0.4 && ResidentSpeech.Greeting(Resident, player) is { } line)
            {
                Mobile.Direction = Mobile.GetDirectionTo(player);
                Mobile.SayTo(player, line);
            }

            return;
        }
    }

    private bool IsPlayerNear(int range)
    {
        foreach (var state in Mobile.Map.GetClientsInRange(Mobile.Location, range))
        {
            if (state.Mobile is { } m && !m.Hidden)
            {
                return true;
            }
        }

        return false;
    }

    // ---------- Danger ----------

    /// <summary>Attacked: run from it, and shout for the guards once.</summary>
    private bool TryFlee()
    {
        var attacker = RecentAttacker();

        if (attacker == null)
        {
            if (_fleeing)
            {
                _fleeing = false;
                Mobile.CurrentSpeed = Mobile.PassiveSpeed;
                _leg = null;
                Resident.Reroute();
            }

            return false;
        }

        if (!_fleeing)
        {
            _fleeing = true;
            Mobile.CurrentSpeed = Mobile.ActiveSpeed;
            Mobile.Say(ResidentSpeech.CryForHelp(Resident));
            Mobile.Region.GetRegion<GuardedRegion>()?.CallGuards(Mobile.Location);
        }

        Mobile.Combatant = null;
        Mobile.Warmode = false;

        var away = attacker.GetDirectionTo(Mobile);

        if (!DoMove(away | Direction.Running, true) &&
            !DoMove((Direction)(((int)away + 1) & 7) | Direction.Running, true))
        {
            DoMove((Direction)(((int)away + 7) & 7) | Direction.Running, true);
        }

        return true;
    }

    private Mobile RecentAttacker()
    {
        if (_monster != null)
        {
            if (Core.TickCount - _monsterUntil < 0 && _monster is { Deleted: false, Alive: true } &&
                _monster.Map == Mobile.Map && Mobile.InRange(_monster, 12))
            {
                return _monster;
            }

            _monster = null;
        }

        var now = Core.Now;
        var aggressors = Mobile.Aggressors;

        for (var i = 0; i < aggressors.Count; i++)
        {
            var info = aggressors[i];
            var attacker = info.Attacker;

            if ((now - info.LastCombatTime).TotalMilliseconds < FleeMemoryMs && attacker?.Alive == true &&
                attacker.Map == Mobile.Map && Mobile.InRange(attacker, 14))
            {
                return attacker;
            }
        }

        return null;
    }
}
