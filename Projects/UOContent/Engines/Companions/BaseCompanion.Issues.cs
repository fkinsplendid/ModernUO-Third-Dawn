using System;
using Server.Regions;

namespace Server.Engines.Companions;

public enum IssueAnswer
{
    Yes,
    No,
    Later
}

/// <summary>
/// Things a companion brings up in a quiet moment: they're worn out, wages are due, the employer
/// keeps dying, they were left dead, they want a horse, they've had enough of what they fear, or
/// they miss home. They wait for "yes", "no" or "later". A yes is a promise, and they remember
/// whether it was kept.
/// </summary>
public abstract partial class BaseCompanion
{
    private const long AnswerWaitMs = 60000;
    private const long CalmAfterFightMs = 15000;
    private const long EmployerDeathWindowMs = 60 * 60 * 1000;
    private const long FearTalkWindowMs = 3 * 60 * 1000;
    private const int HomesickMinutes = 180;
    private const int HomesickMinutesAnyone = 360;

    private CompanionIssue _asked;
    private long _askedAt;
    private CompanionIssue _queued;
    private CompanionIssue _promise;
    private long _promiseMadeAt;
    private long _promiseDeadline;
    private bool _leftDeadPending;
    private readonly long[] _issueCooldowns = new long[(int)CompanionIssue.Notice + 1];
    private readonly int[] _deferrals = new int[(int)CompanionIssue.Notice + 1];
    private readonly long[] _employerDeaths = new long[4];
    private int _employerDeathIndex;

    /// <summary>The question they're waiting on an answer to, if any.</summary>
    public CompanionIssue AskedIssue => _asked;

    /// <summary>What they were promised and are waiting to see, if anything.</summary>
    public CompanionIssue Promise => _promise;

    /// <summary>Not fighting, not just out of a fight, not running, and near the employer.</summary>
    private bool IsCalm =>
        State == CompanionState.Active && !IsDeadPet && Combatant == null && Spell == null &&
        AIObject is CompanionAI ai && !ai.FoughtWithin(CalmAfterFightMs) && Party?.IsRetreating != true &&
        Employer is { } employer && employer.Map == Map && InRange(employer, 12);

    /// <summary>Called every few seconds while alive and working.</summary>
    internal void ThinkMind(long now)
    {
        if (ThinkQuitting())
        {
            return;
        }

        ThinkHome(now);
        ThinkPromise(now);

        if (_asked != CompanionIssue.None)
        {
            if (now - _askedAt >= AnswerWaitMs)
            {
                IssueIgnored(now);
            }

            return;
        }

        if (!IsCalm)
        {
            return;
        }

        if (_queued != CompanionIssue.None)
        {
            RaiseIssue(_queued, now);
            return;
        }

        if (Party != null && now - Party.NextIssue >= 0 && FindIssue(now, false) is var issue and not CompanionIssue.None &&
            (!Quiet || IsSerious(issue)))
        {
            RaiseIssue(issue, now);
        }
    }

    private static bool IsSerious(CompanionIssue issue) => issue is CompanionIssue.Notice or CompanionIssue.WagesShort;

    /// <summary>The most pressing thing on their mind, or None.</summary>
    private CompanionIssue FindIssue(long now, bool asked)
    {
        if (GaveNotice && !_quitting && Ready(CompanionIssue.Notice))
        {
            return CompanionIssue.Notice;
        }

        if (Owed == 0 && Purse < DailyWage && MinutesUntilWage <= 10 && Ready(CompanionIssue.WagesShort))
        {
            return CompanionIssue.WagesShort;
        }

        // While a promise is outstanding they wait to see it kept before asking for more.
        if (_promise != CompanionIssue.None)
        {
            return CompanionIssue.None;
        }

        if (IsExhausted && !Region.IsPartOf<TownRegion>() && Ready(CompanionIssue.Weary))
        {
            return CompanionIssue.Weary;
        }

        if (_leftDeadPending && Ready(CompanionIssue.LeftDead))
        {
            return CompanionIssue.LeftDead;
        }

        if (CountEmployerDeaths(now) >= 2 && Ready(CompanionIssue.EmployerReckless))
        {
            return CompanionIssue.EmployerReckless;
        }

        if (Fear != CompanionFear.None && LastFearFaced != 0 && now - LastFearFaced < FearTalkWindowMs &&
            FearStrength > 0.3 && Ready(CompanionIssue.FacingFear))
        {
            return CompanionIssue.FacingFear;
        }

        if (Steed == null && !StayOnFoot && Employer?.Mounted == true && Ready(CompanionIssue.WantsMount))
        {
            return CompanionIssue.WantsMount;
        }

        var homesickAfter = Fondness == CompanionFondness.Home ? HomesickMinutes : HomesickMinutesAnyone;

        if (!_atHome && _minutesSinceHome >= homesickAfter && Ready(CompanionIssue.MissesHome))
        {
            return CompanionIssue.MissesHome;
        }

        return CompanionIssue.None;

        bool Ready(CompanionIssue issue) => asked || now - _issueCooldowns[(int)issue] >= 0;
    }

    /// <summary>Brings it up now if the moment is right, otherwise as soon as it is.</summary>
    private void RaiseIssue(CompanionIssue issue, long now)
    {
        if (!IsCalm || _asked != CompanionIssue.None)
        {
            _queued = issue;
            return;
        }

        _queued = CompanionIssue.None;
        _asked = issue;
        _askedAt = now;

        if (Party != null)
        {
            Party.NextIssue = now + Utility.RandomMinMax(6, 10) * 60000L;

            // The employer's deaths are everyone's worry, but one of them speaking up is enough.
            if (issue == CompanionIssue.EmployerReckless)
            {
                foreach (var ally in Party.Members)
                {
                    if (ally != null && ally != this)
                    {
                        ally.Cooldown(issue, now, 60);
                    }
                }
            }
        }

        Answer(CompanionSpeech.IssueLine(issue), IssueArg(issue));
        Employer?.SendMessage(0x3B2, $"{Name} is waiting for your answer: yes, no or later.");
    }

    private string IssueArg(CompanionIssue issue) =>
        issue switch
        {
            CompanionIssue.FacingFear  => FearName,
            CompanionIssue.MissesHome  => Hometown,
            CompanionIssue.WagesShort  => MinutesUntilWage.ToString(),
            CompanionIssue.EmployerReckless => CountEmployerDeaths(Core.TickCount).ToString(),
            _                          => null
        };

    /// <summary>"What's on your mind?" Whatever is troubling them, cooldowns or not.</summary>
    public void TellConcerns()
    {
        var now = Core.TickCount;

        if (_asked != CompanionIssue.None)
        {
            _askedAt = now;
            Answer(CompanionSpeech.IssueLine(_asked), IssueArg(_asked));
            return;
        }

        var issue = FindIssue(now, true);

        if (issue == CompanionIssue.None)
        {
            Answer(CompanionSpeech.NothingOnMind(Mood, _promise != CompanionIssue.None));
            return;
        }

        _queued = CompanionIssue.None;
        _asked = issue;
        _askedAt = now;
        Answer(CompanionSpeech.IssueLine(issue), IssueArg(issue));
    }

    /// <summary>The employer's answer to the question on the table. False if nothing was asked.</summary>
    public bool AnswerIssue(IssueAnswer answer)
    {
        var issue = _asked;

        if (issue == CompanionIssue.None)
        {
            return false;
        }

        _asked = CompanionIssue.None;
        var now = Core.TickCount;

        switch (answer)
        {
            case IssueAnswer.Yes:
                {
                    _deferrals[(int)issue] = 0;
                    Answer(CompanionSpeech.IssueReply(issue, answer));
                    Accept(issue, now);
                    break;
                }
            case IssueAnswer.No:
                {
                    _deferrals[(int)issue] = 0;
                    Answer(CompanionSpeech.IssueReply(issue, answer));
                    Refuse(issue, now);
                    break;
                }
            default:
                {
                    // Put off once is fair. Put off again and again is an answer of its own.
                    if (++_deferrals[(int)issue] >= 2)
                    {
                        AdjustRegard(-1);
                    }

                    Answer(CompanionSpeech.IssueReply(issue, answer));
                    Cooldown(issue, now, 8);
                    break;
                }
        }

        return true;
    }

    private void Accept(CompanionIssue issue, long now)
    {
        switch (issue)
        {
            case CompanionIssue.Notice:
                {
                    AdjustRegard(8);
                    Cooldown(issue, now, 10);
                    return;
                }
            case CompanionIssue.LeftDead:
                {
                    _leftDeadPending = false;
                    AdjustRegard(3);
                    return;
                }
        }

        AdjustRegard(1);

        var minutes = issue switch
        {
            CompanionIssue.Weary            => 20,
            CompanionIssue.WagesShort       => Math.Max(1, MinutesUntilWage),
            CompanionIssue.EmployerReckless => 30,
            CompanionIssue.FacingFear       => 10,
            CompanionIssue.WantsMount       => 60,
            _                               => 90
        };

        _promise = issue;
        _promiseMadeAt = now;
        _promiseDeadline = now + minutes * 60000L;
        Cooldown(issue, now, minutes + 10);
    }

    private void Refuse(CompanionIssue issue, long now)
    {
        if (issue == CompanionIssue.Notice)
        {
            _quitting = true;
            return;
        }

        if (issue == CompanionIssue.LeftDead)
        {
            _leftDeadPending = false;
        }

        AdjustRegard(
            issue switch
            {
                CompanionIssue.Weary or CompanionIssue.WagesShort or CompanionIssue.LeftDead => -4,
                _                                                                            => -2
            }
        );

        Cooldown(issue, now, 20);
    }

    /// <summary>Asked and got no answer at all.</summary>
    private void IssueIgnored(long now)
    {
        var issue = _asked;
        _asked = CompanionIssue.None;
        Cooldown(issue, now, 10);

        if (issue != CompanionIssue.Notice)
        {
            AdjustRegard(-1);
        }

        if (Utility.RandomBool())
        {
            Speak(SpeechContext.IssueIgnored);
        }
    }

    private void Cooldown(CompanionIssue issue, long now, int minutes) =>
        _issueCooldowns[(int)issue] = now + minutes * 60000L;

    // ---------- Promises ----------

    private void ThinkPromise(long now)
    {
        switch (_promise)
        {
            case CompanionIssue.None:
                {
                    return;
                }
            case CompanionIssue.Weary when Region.IsPartOf<TownRegion>():
            case CompanionIssue.WantsMount when Steed != null:
                {
                    KeepPromise(_promise);
                    return;
                }
            case CompanionIssue.FacingFear when LastFearFaced - _promiseMadeAt > 0:
                {
                    BreakPromise(_promise);
                    return;
                }
        }

        if (now - _promiseDeadline < 0)
        {
            return;
        }

        // Some promises are kept by nothing happening: no deaths, no more of what they fear.
        if (_promise is CompanionIssue.EmployerReckless or CompanionIssue.FacingFear ||
            _promise == CompanionIssue.WagesShort && Purse >= DailyWage)
        {
            KeepPromise(_promise);
        }
        else
        {
            BreakPromise(_promise);
        }
    }

    private void KeepPromise(CompanionIssue issue)
    {
        if (_promise != issue || issue == CompanionIssue.None)
        {
            return;
        }

        _promise = CompanionIssue.None;
        AdjustRegard(issue == CompanionIssue.MissesHome ? 5 : 3);
        Answer(CompanionSpeech.PromiseLine(issue, true), IssueArg(issue));
    }

    private void BreakPromise(CompanionIssue issue)
    {
        if (_promise != issue || issue == CompanionIssue.None)
        {
            return;
        }

        _promise = CompanionIssue.None;
        AdjustRegard(-4);
        Answer(CompanionSpeech.PromiseLine(issue, false), IssueArg(issue));
    }

    private void ClearIssue(CompanionIssue issue)
    {
        if (_asked == issue)
        {
            _asked = CompanionIssue.None;
        }

        if (_queued == issue)
        {
            _queued = CompanionIssue.None;
        }

        if (_promise == issue)
        {
            _promise = CompanionIssue.None;
        }
    }

    private void ClearIssues()
    {
        _asked = CompanionIssue.None;
        _queued = CompanionIssue.None;
        _promise = CompanionIssue.None;
    }

    private int CountEmployerDeaths(long now)
    {
        var count = 0;

        foreach (var at in _employerDeaths)
        {
            if (at != 0 && now - at < EmployerDeathWindowMs)
            {
                count++;
            }
        }

        return count;
    }
}
