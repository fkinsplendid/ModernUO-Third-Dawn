using System;
using Server.Items;
using Server.Mobiles;
using Server.Regions;

namespace Server.Engines.Companions;

public abstract partial class BaseCompanion
{
    private const int PersonalSpeechDelayMs = 6000;
    private const int UrgentSpeechDelayMs = 2500;

    // Someone standing still this long has the party wondering what they're waiting for.
    private const long WaitingRemarkMs = 150000;

    // Notable foes (dragons, daemons, liches) earn a boast rather than a passing remark.
    private const int BigKillFame = 10000;

    private long _nextSpeech;
    private long _nextIdleCheck;
    private string _place;
    private Point3D _employerSpot;
    private long _employerStillSince;
    private bool _employerWasDead;

    /// <summary>
    /// Says a line for <paramref name="context"/>. Ordinary lines respect the companion's own cooldown,
    /// the party's shared chatter gap and the employer's request for quiet; urgent lines only the first.
    /// </summary>
    public bool Speak(SpeechContext context, string arg = null, bool urgent = false) =>
        (!IsDeadPet || CompanionSpeech.IsGhostLine(context)) &&
        SpeakLine(CompanionSpeech.Pick(context, Personality, Role), arg, urgent);

    /// <summary>Says <paramref name="line"/> under the same rules as <see cref="Speak"/>.</summary>
    private bool SpeakLine(string line, string arg, bool urgent, bool ignorePartyGap = false)
    {
        if (line == null || Deleted || State != CompanionState.Active)
        {
            return false;
        }

        var now = Core.TickCount;

        if (now - _nextSpeech < 0 || !urgent && (Quiet || !ignorePartyGap && Party != null && now - Party.NextChatter < 0))
        {
            return false;
        }

        Say(CompanionSpeech.Format(line, this, arg));

        _nextSpeech = now + (urgent ? UrgentSpeechDelayMs : PersonalSpeechDelayMs);

        if (Party != null && !urgent)
        {
            Party.NextChatter = now + Utility.RandomMinMax(8000, 15000);
        }

        return true;
    }

    /// <summary>A battle cry as a fight opens, often about the foe itself.</summary>
    public bool SpeakBattleCry(Mobile foe)
    {
        var kind = CompanionSpeech.KindOf(foe);

        if (kind != FoeKind.None && Utility.RandomDouble() < 0.6 && !IsDeadPet)
        {
            return SpeakLine(CompanionSpeech.BattleCry(kind), CompanionSpeech.BareName(foe.Name), false);
        }

        return Speak(SpeechContext.CombatStart);
    }

    /// <summary>A remark on a kill: a boast for a notable foe, otherwise often about the foe itself.</summary>
    private void SpeakKill(BaseCreature victim)
    {
        var name = CompanionSpeech.BareName(victim.Name);

        if (victim.Fame >= BigKillFame)
        {
            OnMindBigKill();

            if (Speak(SpeechContext.BigKill, name, urgent: true))
            {
                return;
            }
        }

        var kind = CompanionSpeech.KindOf(victim);

        if (kind != FoeKind.None && Utility.RandomBool() && SpeakLine(CompanionSpeech.KillLine(kind), name, false))
        {
            return;
        }

        Speak(SpeechContext.Kill, name);
    }

    /// <summary>Occasional unprompted remarks about the surroundings, with a chance of a reply.</summary>
    private void ThinkChatter(long now)
    {
        ThinkArrival(now);
        ThinkEmployerRevived(now);

        if (now - _nextIdleCheck < 0)
        {
            return;
        }

        _nextIdleCheck = now + Utility.RandomMinMax(20000, 40000);

        if (Quiet || Combatant != null || Party == null || now - Party.NextChatter < 0 || Utility.RandomDouble() > 0.35)
        {
            return;
        }

        var placeLine = _place != null && Utility.RandomDouble() < 0.4 ? CompanionSpeech.PlaceIdleLine(_place) : null;
        var moodContext = placeLine == null && Utility.RandomDouble() < 0.3 ? MoodIdleContext() : SpeechContext.Count;

        if (!(placeLine != null ? SpeakLine(placeLine, _place, false) :
                Speak(moodContext != SpeechContext.Count ? moodContext : PickIdleContext(now))))
        {
            return;
        }

        // Long gap after idle talk so the party doesn't become a choir.
        Party.NextChatter = now + Utility.RandomMinMax(90000, 180000);

        if (Utility.RandomDouble() < 0.3)
        {
            var replier = PickBanterPartner();

            if (replier != null)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(2, 4)), replier.ReplyToBanter);
            }
        }
    }

    /// <summary>
    /// A remark on reaching a town, dungeon or shrine. One companion speaks for the party, and not
    /// again for the same place for a while. Waits for a lull, but not for the chatter gap.
    /// </summary>
    private void ThinkArrival(long now)
    {
        var place = _place = CompanionSpeech.PlaceOf(Region);

        if (place == null || Party == null || Quiet || Combatant != null || !Party.CanRemarkOn(place, now))
        {
            return;
        }

        var line = CompanionSpeech.ArrivalLine(place, Region.IsPartOf<DungeonRegion>());

        if (SpeakLine(line, place, false, ignorePartyGap: true))
        {
            Party.RemarkedOn(place, now);
            Party.NextChatter = now + Utility.RandomMinMax(60000, 90000);
        }
    }

    /// <summary>A cheer when the employer comes back from the dead. One companion speaks for the party.</summary>
    private void ThinkEmployerRevived(long now)
    {
        var employer = Employer;

        if (employer == null)
        {
            return;
        }

        if (!employer.Alive)
        {
            if (!_employerWasDead)
            {
                _employerWasDead = true;
                OnEmployerDied(now);
            }

            return;
        }

        if (!_employerWasDead)
        {
            return;
        }

        _employerWasDead = false;

        if (Party != null && now - Party.NextCheer < 0 || employer.Map != Map || !InRange(employer, 12))
        {
            return;
        }

        if (Speak(SpeechContext.EmployerRevived, urgent: true) && Party != null)
        {
            Party.NextCheer = now + 10000;
        }
    }

    /// <summary>An idle remark that shows how they feel, or Count if they feel nothing worth airing.</summary>
    private SpeechContext MoodIdleContext()
    {
        if (IsTired)
        {
            return SpeechContext.IdleWeary;
        }

        if (Fear == CompanionFear.TheDark && Region.IsPartOf<DungeonRegion>())
        {
            return SpeechContext.IdleDark;
        }

        return Mood switch
        {
            <= CompanionMood.Unhappy => SpeechContext.IdleUnhappy,
            CompanionMood.Devoted    => SpeechContext.IdleDevoted,
            _                        => SpeechContext.Count
        };
    }

    private void ReplyToBanter() => Speak(SpeechContext.BanterReply, urgent: true);

    private BaseCompanion PickBanterPartner()
    {
        foreach (var ally in Party.Members)
        {
            if (ally != this && CompanionParty.IsFighting(ally) && InRange(ally, 8))
            {
                return ally;
            }
        }

        return null;
    }

    private SpeechContext PickIdleContext(long now)
    {
        if (Region.IsPartOf<DungeonRegion>())
        {
            return SpeechContext.IdleDungeon;
        }

        var inTown = Region.IsPartOf<TownRegion>();

        if (!inTown && EmployerStill(now) && Utility.RandomBool())
        {
            return SpeechContext.Waiting;
        }

        if (inTown)
        {
            return SpeechContext.IdleTown;
        }

        Clock.GetTime(Map, X, Y, out var hours, out int _);

        return hours switch
        {
            5 or 6 when Utility.RandomBool()     => SpeechContext.Dawn,
            18 or 19 when Utility.RandomBool()   => SpeechContext.Dusk,
            >= 21 or < 5 when Utility.RandomBool() => SpeechContext.IdleNight,
            _                                    => SpeechContext.IdleWild
        };
    }

    /// <summary>True once the employer has stood in one spot for a good while.</summary>
    private bool EmployerStill(long now)
    {
        var employer = Employer;

        if (employer == null)
        {
            return false;
        }

        if (employer.Location != _employerSpot)
        {
            _employerSpot = employer.Location;
            _employerStillSince = now;
            return false;
        }

        return now - _employerStillSince >= WaitingRemarkMs;
    }
}
