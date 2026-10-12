using System;
using Server.Items;
using Server.Regions;

namespace Server.Engines.Companions;

public abstract partial class BaseCompanion
{
    private const int PersonalSpeechDelayMs = 6000;
    private const int UrgentSpeechDelayMs = 2500;

    private long _nextSpeech;
    private long _nextIdleCheck;

    /// <summary>
    /// Says a line for <paramref name="context"/>. Ordinary lines respect the companion's own cooldown,
    /// the party's shared chatter gap and the employer's request for quiet; urgent lines only the first.
    /// </summary>
    public bool Speak(SpeechContext context, string arg = null, bool urgent = false)
    {
        if (Deleted || State != CompanionState.Active || IsDeadPet)
        {
            return false;
        }

        var now = Core.TickCount;

        if (now - _nextSpeech < 0 || !urgent && (Quiet || Party != null && now - Party.NextChatter < 0))
        {
            return false;
        }

        var line = CompanionSpeech.Pick(context, Personality);

        if (line == null)
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

    /// <summary>Occasional unprompted remarks about the surroundings, with a chance of a reply.</summary>
    private void ThinkChatter(long now)
    {
        if (now - _nextIdleCheck < 0)
        {
            return;
        }

        _nextIdleCheck = now + Utility.RandomMinMax(20000, 40000);

        if (Quiet || Combatant != null || Party == null || now - Party.NextChatter < 0 || Utility.RandomDouble() > 0.35)
        {
            return;
        }

        if (!Speak(PickIdleContext()))
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

    private SpeechContext PickIdleContext()
    {
        if (Region.IsPartOf<DungeonRegion>())
        {
            return SpeechContext.IdleDungeon;
        }

        if (Region.IsPartOf<TownRegion>())
        {
            return SpeechContext.IdleTown;
        }

        Clock.GetTime(Map, X, Y, out var hours, out int _);

        return hours is >= 21 or < 5 && Utility.RandomBool() ? SpeechContext.IdleNight : SpeechContext.IdleWild;
    }
}
