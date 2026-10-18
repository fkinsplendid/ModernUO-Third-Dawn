using System;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Falling back with the employer. Only the employer can judge when a fight is lost, so the
/// companions take their cue from them: an "all follow me" or a shout of "retreat" in the middle
/// of a fight, or simply seeing them run from it. Then everyone drops what they're doing, casts
/// nothing and plays nothing, and runs to the employer, drinking potions on the way. A mage with
/// something on its heels teleports clear. It's over once nothing has been close behind for a
/// few seconds, and for a while afterwards they defend themselves but don't go back for more.
/// </summary>
public abstract partial class CompanionAI
{
    // Within this of a companion or the employer, an enemy is still giving chase.
    private const int PursuitRange = 10;

    // Pet orders that mean "out of this fight, now": "all follow me" and "<name> follow me".
    private const int AllFollowMeKeyword = 0x16C;
    private const int NamedFollowMeKeyword = 0x163;

    /// <summary>Fighting, or was a moment ago.</summary>
    public bool InFight => _threats.Count > 0 || FoughtWithin(5000);

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);

        if (e.Mobile == Employer && Party != null && InFight &&
            (e.HasKeyword(AllFollowMeKeyword) || e.HasKeyword(NamedFollowMeKeyword)))
        {
            OrderRetreat();
        }
    }

    /// <summary>The employer called the retreat. The first to hear it answers.</summary>
    public void OrderRetreat()
    {
        if (Party.BeginRetreat(Core.TickCount))
        {
            Companion.Speak(SpeechContext.Retreat, urgent: true);
        }
    }

    /// <summary>Takes the tick while the party is falling back. Starts a retreat if the employer is running.</summary>
    private bool TryRetreat()
    {
        var party = Party;
        var employer = Employer;
        var now = Core.TickCount;

        if (employer?.Alive != true || employer.Map != Mobile.Map)
        {
            party.EndRetreat(now);
            return false;
        }

        if (!party.IsRetreating)
        {
            if (_threats.Count == 0 ||
                !party.IsFleeing((employer.Direction & Direction.Running) != 0, NearestThreatTo(employer), now))
            {
                return false;
            }

            if (party.BeginRetreat(now))
            {
                Companion.Speak(SpeechContext.Retreat, urgent: true);
            }
        }

        if (IsPursued(employer))
        {
            party.NotePursued(now);
        }
        else if (party.HasShakenOff(now))
        {
            if (party.EndRetreat(now))
            {
                Companion.Speak(SpeechContext.ShakenOff);
            }

            return false;
        }

        if (_inCombat)
        {
            _inCombat = false;
            _warnedLowHealth = false;
            _warnedEmployerHurt = false;
        }

        if (Mobile.Combatant != null)
        {
            Note("broke off to fall back with you");
        }

        Mobile.Combatant = null;
        Mobile.FocusMob = null;
        Mobile.Warmode = false;

        TrySurvive();

        if (!TryRetreatMove())
        {
            KeepFormation();
        }

        return true;
    }

    /// <summary>A role's own way of getting clear, such as a mage's teleport. Return true to spend the tick.</summary>
    protected virtual bool TryRetreatMove() => false;

    /// <summary>An enemy able to move is close behind this companion or the employer.</summary>
    private bool IsPursued(Mobile employer)
    {
        for (var i = 0; i < _threats.Count; i++)
        {
            var threat = _threats[i];

            if (!threat.Paralyzed && !threat.Frozen &&
                (Mobile.InRange(threat, PursuitRange) || employer.InRange(threat, PursuitRange)))
            {
                return true;
            }
        }

        return false;
    }

    private int NearestThreatTo(Mobile m)
    {
        var nearest = int.MaxValue;

        for (var i = 0; i < _threats.Count; i++)
        {
            nearest = Math.Min(nearest, Distance(_threats[i].Location, m.Location));
        }

        return nearest;
    }
}
