using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Companions look after their own metal gear. Out on the road they mention it when a piece is
/// wearing thin; in town with a <see cref="MasterBlacksmith"/> nearby they walk over and hand it
/// across the counter, one piece at a time, and the smith asks the employer for the bill. A
/// declined bill isn't raised again for a while. "Repair" sends them for anything less than whole.
/// </summary>
public abstract partial class CompanionAI
{
    private const int SmithCheckIntervalMs = 5000;
    private const int SmithSearchRange = 15;

    // How near the smith the employer must be: they settle the bill.
    private const int EmployerAtSmithRange = 10;

    // Worn below this, a companion goes to the smith unasked; below the others, they say so.
    private const double AutoRepairCondition = 0.6;
    private const double WornCondition = 0.5;
    private const double FailingCondition = 0.2;

    private const int SmithReachMs = 30000;
    private const int SmithQueueMs = 120000;
    private const int DeclinedRepairMs = 600000;
    private const int WornRemarkMs = 900000;
    private const int FailingRemarkMs = 300000;

    private MasterBlacksmith _smith;
    private bool _pieceAtSmith;
    private bool _repairOrdered;
    private long _smithGiveUp;
    private long _nextSmithCheck;
    private long _noRepairsUntil;
    private long _nextWearRemark;

    /// <summary>The employer's "repair": go now, for anything less than whole.</summary>
    public void RequestRepair()
    {
        if (_smith != null)
        {
            Mobile.Say("I'm seeing to it already.");
            return;
        }

        if (FindWornPiece(1.0) == null)
        {
            Companion.Speak(SpeechContext.GearSound, urgent: true);
            return;
        }

        if (FindSmith() == null)
        {
            Companion.Speak(SpeechContext.NoSmith, urgent: true);
            return;
        }

        _repairOrdered = true;
        _noRepairsUntil = 0;
        _nextSmithCheck = 0;
    }

    /// <summary>The smith handed a piece back. Called by <see cref="BaseCompanion.OnRepairReturned"/>.</summary>
    internal void OnRepairReturned(RepairOutcome outcome)
    {
        _pieceAtSmith = false;

        // Done with that piece: carry on to the next. Anything else ends the errand.
        if (outcome != RepairOutcome.Repaired)
        {
            _noRepairsUntil = Core.TickCount + DeclinedRepairMs;
            EndSmithErrand();
        }
    }

    private bool TryVisitSmith()
    {
        var now = Core.TickCount;

        if (_smith == null)
        {
            if (now - _nextSmithCheck < 0 || FoughtWithin(10000))
            {
                return false;
            }

            _nextSmithCheck = now + SmithCheckIntervalMs;
            return TryStartSmithErrand(now);
        }

        var smith = _smith;
        var employer = Employer;

        // The smith keeps the piece safe and sends it back wherever we are; only wait for it close by.
        if (_pieceAtSmith)
        {
            if (smith.Deleted || smith.Map != Mobile.Map || !Mobile.InRange(smith, SmithSearchRange))
            {
                return false;
            }

            if (MoveTo(smith, false, 3))
            {
                Mobile.Direction = Mobile.GetDirectionTo(smith);
            }

            return true;
        }

        if (smith.Deleted || smith.Map != Mobile.Map || now - _smithGiveUp >= 0 ||
            employer?.Map != Mobile.Map || !employer.InRange(smith, EmployerAtSmithRange + 4))
        {
            EndSmithErrand();
            return false;
        }

        if (!Mobile.InRange(smith, 2))
        {
            MoveTo(smith, false, 2);
            return true;
        }

        Mobile.Direction = Mobile.GetDirectionTo(smith);

        // Someone else is at the counter: wait our turn.
        if (smith.IsBusy)
        {
            return true;
        }

        var piece = FindWornPiece(_repairOrdered ? 1.0 : AutoRepairCondition);

        if (piece == null)
        {
            EndSmithErrand();
            return false;
        }

        if (smith.OnDragDrop(Mobile, piece))
        {
            _pieceAtSmith = true;
            _smithGiveUp = now + SmithQueueMs;
            Companion.Speak(SpeechContext.HandToSmith, MasterBlacksmith.GetNoun(piece), urgent: true);
            Note($"took my {MasterBlacksmith.GetNoun(piece)} to the smith");
        }
        else
        {
            EndSmithErrand();
        }

        return true;
    }

    private bool TryStartSmithErrand(long now)
    {
        var employer = Employer;

        if (employer?.Alive != true || employer.Map != Mobile.Map || Party.IsRetreating)
        {
            _repairOrdered = false;
            return false;
        }

        var ordered = _repairOrdered;
        var worst = FindWornPiece(1.0);

        if (worst == null)
        {
            _repairOrdered = false;
            return false;
        }

        var condition = Condition(worst);
        var smith = ordered || condition < AutoRepairCondition && now - _noRepairsUntil >= 0 ? FindSmith() : null;

        if (smith == null)
        {
            _repairOrdered = false;
            RemarkOnWear(worst, condition, now);
            return false;
        }

        _smith = smith;
        _smithGiveUp = now + SmithReachMs;
        _nextWearRemark = now + WornRemarkMs;

        if (!ordered)
        {
            Companion.Speak(SpeechContext.ToTheSmith, MasterBlacksmith.GetNoun(worst), urgent: true);
        }
        else
        {
            Mobile.Say("Right away.");
        }

        Note("went to have my gear mended");
        return true;
    }

    private void EndSmithErrand()
    {
        _smith = null;
        _pieceAtSmith = false;
        _repairOrdered = false;
        Path = null;
    }

    private void RemarkOnWear(Item worst, double condition, long now)
    {
        if (condition >= WornCondition || now - _nextWearRemark < 0)
        {
            return;
        }

        var failing = condition < FailingCondition;

        if (Companion.Speak(failing ? SpeechContext.GearFailing : SpeechContext.GearWorn, MasterBlacksmith.GetNoun(worst)))
        {
            _nextWearRemark = now + (failing ? FailingRemarkMs : WornRemarkMs);
        }
    }

    /// <summary>A master smith in town with the employer close enough to settle the bill.</summary>
    private MasterBlacksmith FindSmith()
    {
        var employer = Employer;

        if (employer == null)
        {
            return null;
        }

        foreach (var smith in Mobile.Map.GetMobilesInRange<MasterBlacksmith>(Mobile.Location, SmithSearchRange))
        {
            if (!smith.Deleted && smith.Alive && employer.Map == smith.Map &&
                employer.InRange(smith, EmployerAtSmithRange))
            {
                return smith;
            }
        }

        return null;
    }

    /// <summary>The most worn piece of metal gear being worn, if any is below <paramref name="below"/>.</summary>
    private Item FindWornPiece(double below)
    {
        Item worst = null;
        var worstCondition = below;

        foreach (var item in Mobile.Items)
        {
            if (item is not (BaseWeapon or BaseArmor) || item is BaseWeapon { PoisonCharges: > 0 } ||
                item is not IDurability { MaxHitPoints: > 1 } durability ||
                durability.HitPoints >= durability.MaxHitPoints || !MasterBlacksmith.IsMetalwork(item))
            {
                continue;
            }

            var condition = Condition(item);

            if (condition < worstCondition)
            {
                worst = item;
                worstCondition = condition;
            }
        }

        return worst;
    }

    private static double Condition(Item item) =>
        item is IDurability { MaxHitPoints: > 0 } d ? d.HitPoints / (double)d.MaxHitPoints : 1.0;
}
