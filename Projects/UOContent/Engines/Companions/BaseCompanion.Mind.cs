using System;
using Server.Mobiles;
using Server.Regions;

namespace Server.Engines.Companions;

/// <summary>
/// What a companion thinks and feels, as opposed to what they do.
///
/// Regard (-100 to 100) is how they feel about their employer. Paying on time, praise, raising them
/// from the dead and keeping promises raise it; insults, late wages, leaving them dead and broken
/// promises lower it. Loyal and devoted companions take a little less in wages and will work a day
/// on credit; a bitter one gives notice, and leaves if nothing changes.
///
/// Weariness builds out on the road and in dungeons and clears in town. Each companion also has
/// one fear, conquered by facing it often enough, one fondness, a hometown and a story.
/// </summary>
public abstract partial class BaseCompanion
{
    public const int MinRegard = -100;
    public const int MaxRegard = 100;
    private const int StartingRegard = 10;

    // Regard at which a companion gives notice, and above which they think better of it.
    private const int NoticeRegard = -80;
    private const int WithdrawNoticeRegard = -50;
    private const int QuitRegard = -60;
    private const int NoticeMinutes = 15;

    // A companion who quit and is hired back starts no lower than this.
    private const int RehiredRegard = -30;

    // Weariness is kept in tenths, 0 to 1000.
    private const int MaxWeariness = 1000;
    public const int TiredWeariness = 600;
    public const int ExhaustedWeariness = 850;

    // Times a feared foe must be beaten, or minutes spent below ground, before a fear is conquered.
    private const int FoeFearConquest = 25;
    private const int DarkFearConquest = 120;

    private const long FondnessCooldownMs = 20 * 60 * 1000;
    private const long PraiseCooldownMs = 10 * 60 * 1000;
    private const long GiftCooldownMs = 5 * 60 * 1000;
    private const long ApologyWindowMs = 15 * 60 * 1000;

    // A rest while the employer is away this long counts as a night's sleep.
    private const long RestedAwayMs = 10 * 60 * 1000;

    private int _noticeMinutes;
    private bool _quitting;
    private int _minutesDead;
    private int _minutesSinceHome = Utility.RandomMinMax(60, 180);
    private bool _atHome;
    private long _waitingSince;
    private long _nextFondness;
    private long _nextPraise;
    private long _nextGiftRegard;
    private long _lastSlightAt;
    private int _lastSlight;
    private int _storyTold;

    private static readonly string[] _hometowns =
    [
        "Britain", "Britain", "Trinsic", "Minoc", "Yew", "Moonglow", "Vesper", "Skara Brae", "Jhelom",
        "Magincia", "Cove", "Serpent's Hold", "Buccaneer's Den", "Nujel'm", "Papua", "Delucia", "Ocllo"
    ];

    [CommandProperty(AccessLevel.GameMaster)]
    public CompanionMood Mood =>
        Regard switch
        {
            <= QuitRegard => CompanionMood.Bitter,
            < -20         => CompanionMood.Unhappy,
            < 30          => CompanionMood.Content,
            < 70          => CompanionMood.Loyal,
            _             => CompanionMood.Devoted
        };

    public bool IsTired => Weariness >= TiredWeariness;

    public bool IsExhausted => Weariness >= ExhaustedWeariness;

    /// <summary>How strongly the fear still holds, 1 when fresh, falling towards 0 as it is faced.</summary>
    public double FearStrength =>
        Fear == CompanionFear.None ? 0 : 1.0 - Math.Min(1.0, FearFaced / (double)FearConquest(Fear));

    private static int FearConquest(CompanionFear fear) => fear == CompanionFear.TheDark ? DarkFearConquest : FoeFearConquest;

    /// <summary>Gives a new hire their character: a fear, a fondness, a home and a story.</summary>
    private void RollCharacter()
    {
        _regard = StartingRegard;
        _fear = (CompanionFear)Utility.RandomMinMax(1, (int)CompanionFear.TheDark);
        _fondness = (CompanionFondness)Utility.RandomMinMax(1, (int)CompanionFondness.Home);
        _hometown = _hometowns.RandomElement();
        _story = Utility.Random(int.MaxValue);
    }

    // ---------- Regard ----------

    /// <summary>Raises or lowers regard. Big enough knocks are remembered, so an apology can mend them.</summary>
    public void AdjustRegard(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        Regard = Math.Clamp(Regard + amount, MinRegard, MaxRegard);

        if (amount <= -3)
        {
            _lastSlight = -amount;
            _lastSlightAt = Core.TickCount;
        }

        if (!GaveNotice && Regard <= NoticeRegard && State == CompanionState.Active)
        {
            GaveNotice = true;
            _noticeMinutes = 0;
            RaiseIssue(CompanionIssue.Notice, Core.TickCount);
        }
        else if (GaveNotice && !_quitting && Regard > WithdrawNoticeRegard)
        {
            GaveNotice = false;
            Speak(SpeechContext.NoticeWithdrawn, urgent: true);
        }
    }

    /// <summary>Loyal companions take a little less, as a favour.</summary>
    private int WageDiscountPercent =>
        Mood switch
        {
            CompanionMood.Devoted => 10,
            CompanionMood.Loyal   => 5,
            _                     => 0
        };

    /// <summary>A day's wage was due and the purse was short. Loyal ones carry it on credit, once.</summary>
    private bool TryWorkOnCredit(int wage)
    {
        if (Owed > 0 || Mood < CompanionMood.Loyal)
        {
            return false;
        }

        Owed = wage - Purse;
        Purse = 0;
        AdjustRegard(-8);
        Speak(SpeechContext.OnCredit, Owed.ToString(), urgent: true);
        return true;
    }

    /// <summary>Gold from the employer settles any debt first; the rest goes in the purse.</summary>
    private void ReceiveWages(int amount)
    {
        var wasShort = Purse < DailyWage || Owed > 0;
        var settled = Math.Min(Owed, amount);

        if (settled > 0)
        {
            Owed -= settled;
            amount -= settled;
        }

        Purse += amount;
        _purseWarned = false;

        // Paid when it mattered counts for more than coin dropped on a full purse.
        var days = (settled + amount) / Math.Max(1, DailyWage);
        AdjustRegard(Math.Min(4, days) + (wasShort ? 2 : 0));

        if (settled > 0 && Owed == 0)
        {
            Speak(SpeechContext.DebtSettled, urgent: true);
        }
        else
        {
            Speak(SpeechContext.Paid, (settled + amount).ToString(), urgent: true);
        }

        if (Purse >= DailyWage && Owed == 0)
        {
            KeepPromise(CompanionIssue.WagesShort);
        }
    }

    private void OnGiftReceived()
    {
        var now = Core.TickCount;

        if (now - _nextGiftRegard >= 0)
        {
            _nextGiftRegard = now + GiftCooldownMs;
            AdjustRegard(1);
        }
    }

    // ---------- Words from the employer ----------

    public void OnPraised()
    {
        var now = Core.TickCount;

        if (now - _nextPraise < 0)
        {
            Speak(SpeechContext.PraisedAgain, urgent: true);
            return;
        }

        _nextPraise = now + PraiseCooldownMs;
        AdjustRegard(3);
        Speak(SpeechContext.Praised, urgent: true);
    }

    public void OnInsulted()
    {
        Speak(SpeechContext.Insulted, urgent: true);
        AdjustRegard(-6);
    }

    /// <summary>Mends half of the last real slight, if it was recent and not already forgiven.</summary>
    public void OnApology()
    {
        if (_lastSlight == 0 || Core.TickCount - _lastSlightAt > ApologyWindowMs)
        {
            Speak(SpeechContext.ApologyUnneeded, urgent: true);
            return;
        }

        var mended = (_lastSlight + 1) / 2;
        _lastSlight = 0;
        Speak(SpeechContext.ApologyAccepted, urgent: true);
        AdjustRegard(mended);
    }

    // ---------- Events ----------

    private void OnMindLevelUp() => AdjustRegard(2);

    private void OnMindBigKill()
    {
        AdjustRegard(1);

        if (Fondness == CompanionFondness.Glory)
        {
            FondnessMet();
        }
    }

    internal void OnTreasureFound()
    {
        if (Fondness == CompanionFondness.Treasure)
        {
            FondnessMet();
        }
    }

    private void OnMindDeath() => _minutesDead = 0;

    /// <summary>Glad to be back, less so if they were left lying dead a long while.</summary>
    private void OnMindRevived()
    {
        var neglect = Math.Min(15, _minutesDead - 5);

        AdjustRegard(neglect > 0 ? -neglect : 3);

        if (_minutesDead >= 8)
        {
            _leftDeadPending = true;
        }

        _minutesDead = 0;
    }

    /// <summary>Their employer died. Once is bad luck; again within the hour is carelessness.</summary>
    private void OnEmployerDied(long now)
    {
        var recent = CountEmployerDeaths(now);

        _employerDeaths[_employerDeathIndex] = now;
        _employerDeathIndex = (_employerDeathIndex + 1) % _employerDeaths.Length;

        if (recent >= 1)
        {
            AdjustRegard(-2);
        }

        BreakPromise(CompanionIssue.EmployerReckless);
    }

    private void OnMindReturnToGuild()
    {
        ClearIssues();
        GaveNotice = false;
        _quitting = false;
    }

    private void OnMindResumeService()
    {
        Weariness = 0;

        if (Regard < RehiredRegard)
        {
            Regard = RehiredRegard;
        }
    }

    private void OnMindWaiting() => _waitingSince = Core.TickCount;

    private void OnMindRejoin()
    {
        if (_waitingSince != 0 && Core.TickCount - _waitingSince >= RestedAwayMs)
        {
            Weariness = 0;
        }

        _waitingSince = 0;
    }

    // ---------- The minute ----------

    /// <summary>Weariness, fear of the dark, homesickness and notice. Called once a working minute.</summary>
    private void TickMind()
    {
        if (IsDeadPet)
        {
            _minutesDead++;
            return;
        }

        var region = Region;
        var inTown = region.IsPartOf<TownRegion>();
        var inDungeon = region.IsPartOf<DungeonRegion>();
        var fought = AIObject is CompanionAI ai && ai.FoughtRecently;

        var change = inTown && !fought ? -40 :
            inDungeon ? Fear == CompanionFear.TheDark ? 25 : 15 :
            Fondness == CompanionFondness.OpenRoad ? 3 : 6;

        if (fought)
        {
            change += 5;
        }

        if (_atHome)
        {
            change -= 40;
        }

        var wasMaxed = Weariness >= MaxWeariness;
        Weariness = Math.Clamp(Weariness + change, 0, MaxWeariness);

        // Run ragged for ten minutes at a stretch, and it starts to tell.
        if (wasMaxed && Weariness >= MaxWeariness && MinutesServed % 10 == 0)
        {
            AdjustRegard(-1);
        }

        if (inDungeon && Fear == CompanionFear.TheDark)
        {
            FaceFear();
        }

        if (!_atHome)
        {
            _minutesSinceHome++;
        }

        TickFondness(inTown, inDungeon);

        if (GaveNotice)
        {
            _noticeMinutes++;
        }
    }

    private void TickFondness(bool inTown, bool inDungeon)
    {
        var met = Fondness switch
        {
            CompanionFondness.OpenRoad => !inTown && !inDungeon,
            CompanionFondness.Music    => HasBardNearby(),
            _                          => false
        };

        if (met && Utility.Random(10) == 0)
        {
            FondnessMet();
        }
    }

    private bool HasBardNearby() =>
        Party?[CompanionRole.Bard] is { } bard && CompanionParty.IsFighting(bard) && bard.Map == Map && InRange(bard, 12);

    /// <summary>Leaves once the notice has run out, or at once if told to go, but never mid-fight or as a ghost.</summary>
    private bool ThinkQuitting()
    {
        if (!_quitting && !(GaveNotice && _noticeMinutes >= NoticeMinutes && Regard <= QuitRegard))
        {
            return false;
        }

        if (IsDeadPet || Combatant != null || AIObject is CompanionAI { InFight: true })
        {
            return false;
        }

        var employer = Employer;
        ReturnToGuild(SpeechContext.Quits);
        employer?.SendMessage(0x22, $"{Name} has left your service and gone back to the guild.");
        return true;
    }

    /// <summary>Something they love: a word about it and a lift in spirits, now and then.</summary>
    private void FondnessMet()
    {
        var now = Core.TickCount;

        if (now - _nextFondness < 0)
        {
            return;
        }

        _nextFondness = now + FondnessCooldownMs;
        AdjustRegard(1);
        SpeakLine(CompanionSpeech.FondnessLine(Fondness), Hometown, false, ignorePartyGap: true);
    }

    /// <summary>Notices reaching or leaving their hometown.</summary>
    private void ThinkHome(long now)
    {
        var atHome = _place != null && _place.InsensitiveEquals(Hometown);

        if (atHome == _atHome)
        {
            return;
        }

        _atHome = atHome;

        if (!atHome)
        {
            return;
        }

        var longAway = _minutesSinceHome >= 60;
        _minutesSinceHome = 0;
        KeepPromise(CompanionIssue.MissesHome);

        if (Fondness == CompanionFondness.Home)
        {
            FondnessMet();
        }
        else if (longAway && Combatant == null)
        {
            SpeakLine(CompanionSpeech.HomecomingLine(), Hometown, false, ignorePartyGap: true);
        }
    }

    // ---------- Fear ----------

    public bool Fears(Mobile foe) => Fear != CompanionFear.None && CompanionSpeech.FearOf(foe) == Fear;

    public string FearName => CompanionSpeech.FearName(Fear);

    /// <summary>Last time they had to face what they fear, for bringing it up afterwards.</summary>
    public long LastFearFaced { get; private set; }

    internal void NoteFearSighted() => LastFearFaced = Core.TickCount;

    /// <summary>One more time faced. Enough of them and the fear is gone for good.</summary>
    private void FaceFear()
    {
        if (Fear == CompanionFear.None)
        {
            return;
        }

        FearFaced++;

        if (FearFaced < FearConquest(Fear))
        {
            return;
        }

        var name = FearName;

        ConqueredFear = Fear;
        Fear = CompanionFear.None;
        FearFaced = 0;
        ClearIssue(CompanionIssue.FacingFear);

        Speak(SpeechContext.FearConquered, name, urgent: true);
        AdjustRegard(5);
    }
}
