using System.Collections.Generic;
using Server.Items;
using Server.Multis;
using Server.Regions;
using Server.Spells;
using Server.Spells.Third;
using Server.Targeting;

namespace Server.Engines.Companions;

public sealed partial class MageCompanionAI
{
    private const int UnlockScanMs = 3000;

    private long _nextUnlockScan;
    private Spell _unlockSpell;
    private LockableContainer _unlockChest;

    // Chests whose lock shrugged off the spell, and the strength of the spell that failed. Only a
    // stronger mage tries them again.
    private Dictionary<LockableContainer, int> _locksTooHard;

    /// <summary>How strong a lock this mage's Unlock can open, as the spell reckons it.</summary>
    private int UnlockLevel => (int)(Magery * 0.8) - 4;

    /// <summary>
    /// Between fights in a dungeon, casts Unlock on the nearest locked chest in sight. The mage
    /// only finds out how strong a lock is by trying it; one that holds is left alone until the
    /// mage's magic has grown. Opening it, traps and all, is left to the employer.
    /// </summary>
    private bool TryUnlock()
    {
        var now = Core.TickCount;

        if (now - _nextUnlockScan < 0 || Threats.Count > 0 || FoughtWithin(5000) || _gateDeadline != 0 ||
            _markSpell != null || _unlockSpell != null || Mobile.Mana < Mobile.ManaMax / 2 ||
            !Mobile.Region.IsPartOf<DungeonRegion>())
        {
            return false;
        }

        _nextUnlockScan = now + UnlockScanMs;

        var spell = new UnlockSpell(Mobile);

        return Affordable(spell) != null && FindLockedChest(UnlockRange(spell)) is { } chest && BeginUnlock(spell, chest);
    }

    private static int UnlockRange(UnlockSpell spell) => ((ITargetingSpell<IPoint3D>)spell).TargetRange;

    private bool BeginUnlock(UnlockSpell spell, LockableContainer chest)
    {
        if (!CastOnItem(spell, chest))
        {
            return false;
        }

        _unlockSpell = spell;
        _unlockChest = chest;
        Companion.Speak(SpeechContext.UnlockStart, urgent: true);
        return true;
    }

    /// <summary>
    /// The employer's "unlock": they point out the lock, anywhere, and the mage tries it. Says why
    /// not if it can't, before and after the pointing, so no reagents go on a cast that can't land.
    /// </summary>
    public void RequestUnlock(Mobile employer)
    {
        var refusal = UnlockRefusal(new UnlockSpell(Mobile));

        if (refusal != null)
        {
            Mobile.Say(refusal);
            return;
        }

        Mobile.Say("Show me the lock.");
        employer.BeginTarget(-1, false, TargetFlags.None, OnUnlockPointedOut, this);
    }

    private static void OnUnlockPointedOut(Mobile from, object targeted, MageCompanionAI ai) => ai.UnlockPointedOut(from, targeted);

    private void UnlockPointedOut(Mobile from, object targeted)
    {
        if (from != Employer || Companion.Deleted || Companion.IsDeadPet)
        {
            return;
        }

        var spell = new UnlockSpell(Mobile);
        var refusal = UnlockRefusal(spell) ?? UnlockRefusal(spell, targeted as LockableContainer);

        if (refusal == null && !BeginUnlock(spell, (LockableContainer)targeted))
        {
            refusal = "I can't cast just now.";
        }

        if (refusal != null)
        {
            Mobile.Say(refusal);
        }
    }

    /// <summary>Why the mage can't cast Unlock at all just now, or null if it can.</summary>
    private string UnlockRefusal(UnlockSpell spell)
    {
        if (Threats.Count > 0)
        {
            return "Not in the middle of a fight!";
        }

        if (Mobile.Spell != null || _gateDeadline != 0 || _markSpell != null || _unlockSpell != null)
        {
            return "Give me a moment, I'm already casting.";
        }

        if (!KnowsSpell(spell))
        {
            return $"Unlock's not in my book yet. I'll learn it at {CompanionMage.CircleSkill(3):0} Magery.";
        }

        if (!HasReagents(spell.Info))
        {
            return "I'm short of reagents for that. It takes blood moss and sulfurous ash.";
        }

        return Mobile.Mana < spell.GetMana() ? "I'm spent. Let me rest, then ask me again." : null;
    }

    /// <summary>Why the mage won't cast Unlock on <paramref name="chest"/>, or null if it will.</summary>
    internal string UnlockRefusal(UnlockSpell spell, LockableContainer chest)
    {
        if (chest == null || chest.LockLevel == ILockpickable.CannotPick)
        {
            return "That's no lock my magic can open.";
        }

        if (!chest.Locked)
        {
            return "That isn't locked.";
        }

        if (chest.Parent != null)
        {
            return "Set it down where I can see it.";
        }

        if (BaseHouse.CheckSecured(chest))
        {
            return "That's secured. My spell won't touch it.";
        }

        if (_locksTooHard?.TryGetValue(chest, out var tried) == true && tried >= UnlockLevel)
        {
            return "I've tried that lock already. It's beyond my magic.";
        }

        return chest.Map != Mobile.Map || !Mobile.InRange(chest, UnlockRange(spell)) || !Mobile.InLOS(chest)
            ? "I can't reach it from here. Bring me closer."
            : null;
    }

    internal LockableContainer FindLockedChest(int range)
    {
        PruneLocksTooHard();

        LockableContainer best = null;
        var bestDistance = double.MaxValue;
        var level = UnlockLevel;

        foreach (var chest in Mobile.Map.GetItemsInRange<LockableContainer>(Mobile.Location, range))
        {
            if (!chest.Locked || chest.LockLevel == ILockpickable.CannotPick || !chest.Visible ||
                _locksTooHard?.TryGetValue(chest, out var tried) == true && tried >= level ||
                BaseHouse.CheckSecured(chest) || !Mobile.InLOS(chest))
            {
                continue;
            }

            var distance = Mobile.GetDistanceToSqrt(chest);

            if (distance < bestDistance)
            {
                best = chest;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Once Unlock has run its course, says how it went. A lock that held against a spell that
    /// went off is remembered as too strong; a fizzle or an interrupted cast is simply tried again.
    /// </summary>
    private void CheckUnlockLanded()
    {
        if (_unlockSpell == null || _unlockSpell.State != SpellState.None || Mobile.Spell == _unlockSpell)
        {
            return;
        }

        var chest = _unlockChest;
        _unlockSpell = null;
        _unlockChest = null;

        if (chest?.Deleted != false)
        {
            return;
        }

        if (!chest.Locked)
        {
            _locksTooHard?.Remove(chest);
            Companion.Speak(SpeechContext.Unlocked, urgent: true);
        }
        else if (!SpellCanOpen(chest))
        {
            (_locksTooHard ??= new Dictionary<LockableContainer, int>())[chest] = UnlockLevel;
            Companion.Speak(SpeechContext.LockTooHard, urgent: true);
        }
        else
        {
            Companion.Speak(SpeechContext.Fizzle, urgent: true);
        }
    }

    // Mirrors UnlockSpell.Target: whether the spell, once it goes off, beats this lock.
    internal bool SpellCanOpen(LockableContainer chest) =>
        UnlockLevel >= chest.RequiredSkill && !(chest is TreasureMapChest map && map.Level > 2);

    private void PruneLocksTooHard()
    {
        if (_locksTooHard == null)
        {
            return;
        }

        List<LockableContainer> gone = null;

        foreach (var (chest, _) in _locksTooHard)
        {
            if (chest.Deleted || !chest.Locked)
            {
                (gone ??= new List<LockableContainer>()).Add(chest);
            }
        }

        if (gone == null)
        {
            return;
        }

        foreach (var chest in gone)
        {
            _locksTooHard.Remove(chest);
        }
    }
}
