using System;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Mounts. The employer hands one over with the ordinary Transfer order and the companion owns it
/// from then on: rides it everywhere, fights from the saddle, keeps it fed, leads it through gates,
/// and climbs back on after being revived. A mount they can't handle, or one that won't carry them,
/// is turned down. If it's killed they grieve, and later ask for another.
/// </summary>
public abstract partial class BaseCompanion
{
    // Seconds per tile, matching a player on foot and on horseback.
    private const double FootActiveSpeed = 0.2;
    private const double FootPassiveSpeed = 0.4;
    private const double RiddenActiveSpeed = 0.1;
    private const double RiddenPassiveSpeed = 0.2;

    private const double MinSteedControlChance = 0.5;
    private const int SteedTendRange = 20;
    private const int SteedLeadRange = 12;
    private const int SteedCallIntervalMs = 15 * 60 * 1000;
    private const int SteedSickCallIntervalMs = 2 * 60 * 1000;

    private bool _lostSteed;
    private long _nextSteedCall;
    private long _nextSteedSickCall;

    public bool IsRiding => _steed != null && _steed.Rider == this;

    /// <summary>
    /// Takes a pet the employer is handing over. Called from the pet's Transfer order, so the pet
    /// is already known to obey <paramref name="from"/>.
    /// </summary>
    public void AcceptSteed(BaseCreature pet, Mobile from)
    {
        var refusal = GetSteedRefusal(pet, from);

        if (refusal != null)
        {
            if (IsDeadPet)
            {
                from.SendMessage($"{Name} can't take anything in that state.");
            }
            else
            {
                SayTo(from, refusal);
            }

            pet.ControlTarget = from;
            pet.ControlOrder = OrderType.Follow;
            return;
        }

        var steed = (BaseMount)pet;
        var wasBonded = steed.IsBonded;

        if (!steed.SetControlMaster(this))
        {
            SayTo(from, "I can't manage another animal.");
            steed.ControlTarget = from;
            steed.ControlOrder = OrderType.Follow;
            return;
        }

        steed.IsBonded = false;
        steed.BondingBegin = DateTime.MinValue;
        steed.OwnerAbandonTime = DateTime.MinValue;
        steed.Loyalty = MaxLoyalty;
        steed.Combatant = null;
        steed.Warmode = false;
        steed.ControlTarget = this;
        steed.ControlOrder = OrderType.Follow;

        Steed = steed;
        StayOnFoot = false;
        _lostSteed = false;

        Speak(SpeechContext.SteedGiven, urgent: true);

        if (wasBonded)
        {
            from.SendMessage(0x59, $"{steed.Name} is no longer bonded to you.");
        }
    }

    private string GetSteedRefusal(BaseCreature pet, Mobile from)
    {
        if (from != Employer || State != CompanionState.Active)
        {
            return "I only take coin and orders from my employer.";
        }

        if (IsDeadPet)
        {
            return "";
        }

        if (pet is not BaseMount mount)
        {
            return $"Kind of you, {from.Name}, but I can't ride that.";
        }

        if (HasLivingSteed)
        {
            return "I've a mount already. Keep that one for yourself.";
        }

        if (mount.Summoned)
        {
            return "That won't be here come morning. Keep it.";
        }

        if (Female ? !mount.AllowFemaleRider : !mount.AllowMaleRider)
        {
            return "It won't let me anywhere near it.";
        }

        // Pre-AOS rules never drop the odds to nothing, so judge it as a rider would: a beast that
        // would ignore them half the time isn't one to sit on.
        if (mount.GetControlChance(this) < MinSteedControlChance)
        {
            return "That beast would throw me before we'd gone ten paces. I haven't the knack with animals.";
        }

        if (Combatant != null || mount.Combatant != null || mount.Aggressors.Count > 0 || mount.Aggressed.Count > 0 ||
            Core.TickCount < mount.NextCombatTime)
        {
            return "Not now! Hand it over when the fighting's done.";
        }

        if (Followers + mount.ControlSlots > FollowersMax)
        {
            return "I can't manage another animal.";
        }

        return null;
    }

    private bool HasLivingSteed => _steed is { Deleted: false, Alive: true } steed && steed.ControlMaster == this;

    /// <summary>Their own steed is here, healthy and free to ride.</summary>
    public bool CanMount(BaseMount steed) =>
        steed is { Deleted: false, Alive: true, IsDeadPet: false, Rider: null, Poisoned: false } &&
        steed == _steed && steed.ControlMaster == this && steed.Map == Map && !IsDeadPet && !Mounted;

    public void MountSteed()
    {
        var steed = _steed;

        if (!CanMount(steed))
        {
            return;
        }

        steed.Rider = this;
        UpdatePace();
    }

    /// <summary>Gets down and leaves the steed standing where it is.</summary>
    public bool Dismount(bool urgent)
    {
        StayOnFoot = true;

        var steed = _steed;

        if (steed?.Rider != this)
        {
            return false;
        }

        steed.Rider = null;
        steed.ControlTarget = this;
        steed.ControlOrder = OrderType.Stay;
        UpdatePace();

        Speak(SpeechContext.Dismount, urgent: urgent);
        return true;
    }

    /// <summary>Climbs back on the next time the steed is within reach.</summary>
    public bool MountUp(bool urgent)
    {
        StayOnFoot = false;

        var steed = _steed;

        if (!HasLivingSteed || steed.Rider == this)
        {
            return false;
        }

        steed.ControlTarget = this;
        steed.ControlOrder = OrderType.Follow;

        if (steed.Map != Map || !InRange(steed, SteedLeadRange))
        {
            SayTo(Employer, "My mount's back where we left it.");
            return true;
        }

        Speak(SpeechContext.MountUp, urgent: urgent);
        return true;
    }

    /// <summary>The employer asks for their mount back.</summary>
    public bool HandBackSteed()
    {
        var employer = Employer;

        if (!HasLivingSteed)
        {
            SayTo(employer, "I've no mount to give you.");
            return false;
        }

        if (employer.Followers + _steed.ControlSlots > employer.FollowersMax)
        {
            SayTo(employer, $"You've no room for another animal, {employer.Name}.");
            return false;
        }

        ReturnSteed(false);
        Speak(SpeechContext.SteedReturned, urgent: true);
        return true;
    }

    /// <summary>
    /// Gives the steed back to the employer. When the contract is ending and they have no room for
    /// it, it goes to their stable instead; with no employer at all, it goes with the companion.
    /// </summary>
    public bool ReturnSteed(bool contractEnding)
    {
        var steed = _steed;

        if (steed == null)
        {
            return false;
        }

        Steed = null;

        if (steed.Deleted || !steed.Alive || steed.ControlMaster != this)
        {
            return false;
        }

        var employer = Employer;

        if (employer?.Deleted != false)
        {
            steed.Delete();
            return false;
        }

        var hasRoom = employer.Followers + steed.ControlSlots <= employer.FollowersMax;

        if (!hasRoom && !contractEnding)
        {
            Steed = steed;
            return false;
        }

        if (steed.Rider == this)
        {
            steed.Rider = null;
        }

        steed.Combatant = null;
        steed.Warmode = false;
        steed.Loyalty = MaxLoyalty;

        if (hasRoom && steed.SetControlMaster(employer))
        {
            // From the guild hall the stable hands bring it round.
            if (steed.Map != employer.Map || !steed.InRange(employer, SteedLeadRange))
            {
                steed.MoveToWorld(employer.Location, employer.Map);
            }

            steed.ControlTarget = employer;
            steed.ControlOrder = OrderType.Follow;
            employer.SendMessage(0x59, $"{Name} hands the reins of {steed.Name} back to you.");
            return true;
        }

        if (employer is PlayerMobile player)
        {
            steed.ControlTarget = null;
            steed.ControlOrder = OrderType.Stay;
            steed.Internalize();
            steed.SetControlMaster(null);
            steed.IsStabled = true;
            steed.StabledBy = employer;
            player.AddStabled(steed);

            employer.SendMessage(
                0x59,
                $"You have no room for {steed.Name}, so it has been left at the stables. Claim it from any animal trainer."
            );
            return true;
        }

        steed.Delete();
        return false;
    }

    /// <summary>A ridden steed goes everywhere its rider does. One left standing is taken along too.</summary>
    private void StowSteed()
    {
        var steed = _steed;

        if (steed?.Deleted != false || steed.Rider == this || steed.ControlMaster != this)
        {
            return;
        }

        steed.Combatant = null;
        steed.Warmode = false;
        steed.ControlOrder = OrderType.Stay;
        steed.Internalize();
    }

    /// <summary>Brings an unridden steed to the companion's side, after a logout, a gate or catching up.</summary>
    internal void BringSteed()
    {
        var steed = _steed;

        if (steed?.Deleted != false || !steed.Alive || steed.Rider != null || steed.ControlMaster != this ||
            Map == null || Map == Map.Internal)
        {
            return;
        }

        if (steed.Map != Map || !InRange(steed, 2))
        {
            steed.MoveToWorld(Location, Map);
        }

        steed.ControlTarget = this;
        steed.ControlOrder = StayOnFoot ? OrderType.Stay : OrderType.Follow;
    }

    /// <summary>The steed is walking beside them, unridden, and would come along if they moved on.</summary>
    internal bool IsLeadingSteed() =>
        _steed is { Deleted: false, Alive: true, Rider: null, ControlOrder: OrderType.Follow } steed &&
        steed.ControlMaster == this && steed.Map == Map && InRange(steed, SteedLeadRange);

    /// <summary>Death throws the rider. The steed stays by them and trails their ghost.</summary>
    private void OnRiderDied()
    {
        var steed = _steed;

        if (steed is { Deleted: false, Alive: true, Rider: null } && steed.ControlMaster == this)
        {
            steed.ControlTarget = this;
            steed.ControlOrder = OrderType.Follow;
        }
    }

    /// <summary>Mounted companions move at a mounted player's pace.</summary>
    private void UpdatePace()
    {
        var mounted = Mounted;
        var active = mounted ? RiddenActiveSpeed : FootActiveSpeed;

        if (Math.Abs(ActiveSpeed - active) < 0.0001)
        {
            return;
        }

        ActiveSpeed = active;
        PassiveSpeed = mounted ? RiddenPassiveSpeed : FootPassiveSpeed;
        CurrentSpeed = Warmode || Combatant != null ? ActiveSpeed : PassiveSpeed;
    }

    /// <summary>Looks after the steed, notices when it's lost, and asks for a new one now and then.</summary>
    internal void CheckSteed(long now)
    {
        var steed = _steed;

        if (steed == null)
        {
            if (_lostSteed && Combatant == null && now - _nextSteedCall >= 0 && Speak(SpeechContext.SteedWanted))
            {
                _nextSteedCall = now + SteedCallIntervalMs;
            }

            return;
        }

        if (steed.Deleted || !steed.Alive)
        {
            Steed = null;
            _lostSteed = true;
            _nextSteedCall = now + SteedCallIntervalMs;
            Speak(SpeechContext.SteedLost, urgent: true);
            return;
        }

        if (steed.ControlMaster != this)
        {
            Steed = null;
            return;
        }

        if (steed.Rider == this)
        {
            return;
        }

        if (steed.Map == Map && InRange(steed, SteedTendRange))
        {
            steed.Loyalty = MaxLoyalty;

            if (steed.Poisoned && !StayOnFoot && now - _nextSteedSickCall >= 0 && Speak(SpeechContext.SteedSick))
            {
                _nextSteedSickCall = now + SteedSickCallIntervalMs;
            }
        }
    }
}
