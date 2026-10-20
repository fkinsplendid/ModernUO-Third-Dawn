using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Server.Spells.Fifth;
using Server.Spells.Fourth;
using Server.Spells.Sixth;
using Server.Targeting;

namespace Server.Engines.Companions;

/// <summary>
/// The shared brain of every companion. Each tick it works down a fixed list of priorities and
/// stops at the first one that wants the turn:
///
///   finish a spell → follow a party gate → fall back with the employer → drink a potion → role
///   support (heal, cure, sing, buff, revive) → get back on their mount → fight whatever threatens
///   the party → loot → share → ordinary pet orders.
///
/// Threats come from the aggressor lists of the employer and the companions, never from scanning
/// the area, so a quiet tick costs almost nothing.
/// </summary>
public abstract partial class CompanionAI : BaseAI
{
    private const int PerceptionIntervalMs = 400;
    private const int LeashRange = 14;

    // An employer whose target is within this is still fighting it (archers and mages included).
    private const int EmployerFightRange = 12;
    private const int ThreatRange = 16;

    // A typical blow in a fight; a foe hitting this hard counts at its full hit points.
    private const double ReferenceDamage = 15.0;
    private static readonly long RecentCombatMs = 20000;

    private readonly List<Mobile> _threats = new(8);
    private long _nextPerception;
    private long _nextLootAction;
    private long _nextShare;
    private long _nextPotion;
    private long _nextLadenCall;
    private Corpse _lootCorpse;
    private long _lootGiveUp;
    private IPoint3D _spellTarget;
    private SpellInfo _spellReagents;
    private bool _inCombat;
    private long _lastThreatSeen;
    private bool _warnedLowHealth;
    private bool _warnedEmployerHurt;
    private bool _warnedPoisoned;

    protected CompanionAI(BaseCompanion companion) : base(companion) => Companion = companion;

    public BaseCompanion Companion { get; }

    protected CompanionParty Party => Companion.Party;

    protected Mobile Employer => Companion.Employer;

    /// <summary>Everything currently fighting the party, gathered at most every few hundred ms.</summary>
    protected List<Mobile> Threats => _threats;

    /// <summary>True if anything has threatened the party within the last <paramref name="ms"/> milliseconds.</summary>
    protected bool FoughtWithin(int ms) => _lastThreatSeen != 0 && Core.TickCount - _lastThreatSeen < ms;

    /// <summary>Combined health of the employer and living companions, 0 to 1.</summary>
    protected double PartyHealth { get; private set; } = 1.0;

    /// <summary>Enemy hit points divided by party hit points.</summary>
    protected double EnemyStrength { get; private set; }

    /// <summary>The fight is being lost badly enough that getting out beats fighting on.</summary>
    protected bool InSeriousTrouble =>
        _threats.Count > 0 && (PartyHealth < 0.35 || EnemyStrength > 3.0 ||
                               Employer is { Alive: true } e && e.Hits < e.HitsMax / 4 && _threats.Count > 1);

    public override bool Obey()
    {
        if (Mobile.Deleted)
        {
            return false;
        }

        // Anyone not working just obeys plain orders.
        if (Party == null || Companion.State != CompanionState.Active)
        {
            return base.Obey();
        }

        UpdatePerception();

        if (Companion.IsDeadPet)
        {
            return ObeyAsGhost();
        }

        _diedAt = 0;

        if (ContinueCasting() || Mobile.Spell?.IsCasting == true)
        {
            return true;
        }

        if (TryFollowGate() || TryRetreat() || TrySurvive() || TryRoleSupport() || TryMount())
        {
            return true;
        }

        var order = Mobile.ControlOrder;

        if (order == OrderType.Attack)
        {
            return base.Obey();
        }

        var defends = order is OrderType.Follow or OrderType.Guard or OrderType.Come or OrderType.None or OrderType.Stay;

        if (defends && _threats.Count > 0 && EngageThreats(order == OrderType.Stay))
        {
            return true;
        }

        if (_inCombat && _threats.Count == 0)
        {
            EndCombat();
        }

        if (TryRoleIdle() || TryLoot() || TryShare())
        {
            return true;
        }

        return base.Obey();
    }

    // ---------- Role hooks ----------

    /// <summary>Heals, cures, songs, buffs. Return true to spend the tick.</summary>
    protected virtual bool TryRoleSupport() => false;

    /// <summary>Out-of-combat role behaviour (playing music, opening a town gate).</summary>
    protected virtual bool TryRoleIdle() => false;

    /// <summary>Picks who to fight. Default: stay on the current target, else the nearest threat.</summary>
    protected virtual Mobile SelectTarget(bool holdPosition)
    {
        var current = Mobile.Combatant;

        if (current != null && _threats.Contains(current) && (!holdPosition || Mobile.InRange(current, 2)))
        {
            return current;
        }

        return FindNearestThreat(holdPosition ? 2 : ThreatRange);
    }

    // ---------- Perception ----------

    private void UpdatePerception()
    {
        var now = Core.TickCount;

        if (now - _nextPerception < 0)
        {
            return;
        }

        _nextPerception = now + PerceptionIntervalMs;
        _threats.Clear();

        var partyHits = 0;
        var partyMax = 0;
        var employer = Employer;

        if (employer?.Alive == true && employer.Map == Mobile.Map)
        {
            CollectThreats(employer);
            partyHits += employer.Hits;
            partyMax += employer.HitsMax;
        }

        foreach (var member in Party.Members)
        {
            if (CompanionParty.IsFighting(member) && member.Map == Mobile.Map)
            {
                CollectThreats(member);
                partyHits += member.Hits;
                partyMax += member.HitsMax;
            }
        }

        if (_threats.Count > 0)
        {
            _lastThreatSeen = now;
        }

        var enemyHits = 0.0;

        for (var i = 0; i < _threats.Count; i++)
        {
            enemyHits += _threats[i].Hits * HitHarder(_threats[i]);
        }

        PartyHealth = partyMax > 0 ? partyHits / (double)partyMax : 0;
        EnemyStrength = enemyHits / (double)(partyHits > 0 ? partyHits : 1);

        ReadFight();
        CheckWarnings();
    }

    /// <summary>
    /// How hard a foe hits, against a typical blow of <see cref="ReferenceDamage"/>. Hit points alone
    /// make a pack of ettins look deadlier than an ogre lord; weighing them by their blows doesn't.
    /// Spellcasters count as at least typical, since their melee damage says nothing of their spells.
    /// </summary>
    private static double HitHarder(Mobile m)
    {
        if (m is not BaseCreature { DamageMin: > 0, DamageMax: > 0 } creature)
        {
            return 1.0;
        }

        var factor = Math.Clamp((creature.DamageMin + creature.DamageMax) / 2.0 / ReferenceDamage, 0.5, 2.5);
        return IsSpellcaster(m) ? Math.Max(factor, 1.0) : factor;
    }

    private void CollectThreats(Mobile member)
    {
        AddThreat(member.Combatant);

        var now = Core.Now;
        var aggressors = member.Aggressors;

        for (var i = 0; i < aggressors.Count; i++)
        {
            var info = aggressors[i];

            if ((now - info.LastCombatTime).TotalMilliseconds < RecentCombatMs)
            {
                AddThreat(info.Attacker);
            }
        }

        var aggressed = member.Aggressed;

        for (var i = 0; i < aggressed.Count; i++)
        {
            var info = aggressed[i];

            if ((now - info.LastCombatTime).TotalMilliseconds < RecentCombatMs)
            {
                AddThreat(info.Defender);
            }
        }
    }

    private void AddThreat(Mobile m)
    {
        if (m?.Deleted != false || !m.Alive || m.IsDeadBondedPet || m.Map != Mobile.Map ||
            m.AccessLevel > AccessLevel.Player || m is BaseCreature { BardPacified: true } ||
            !Mobile.InRange(m, ThreatRange) || Party.IsFriendly(m) || !Mobile.CanSee(m) ||
            !Mobile.CanBeHarmful(m, false) || _threats.Contains(m))
        {
            return;
        }

        _threats.Add(m);
    }

    private void CheckWarnings()
    {
        if (_threats.Count == 0)
        {
            return;
        }

        if (!Mobile.Poisoned)
        {
            _warnedPoisoned = false;
        }
        else if (!_warnedPoisoned)
        {
            _warnedPoisoned = Companion.Speak(SpeechContext.Poisoned, urgent: true);
        }

        if (!_warnedLowHealth && Mobile.Hits < Mobile.HitsMax * 0.3)
        {
            _warnedLowHealth = Companion.Speak(SpeechContext.LowHealth, urgent: true);
        }

        var employer = Employer;

        if (!_warnedEmployerHurt && employer is { Alive: true } && employer.Hits < employer.HitsMax * 0.4 &&
            Mobile.InRange(employer, 10))
        {
            _warnedEmployerHurt = Companion.Speak(SpeechContext.EmployerHurt, urgent: true);
        }
    }

    protected Mobile FindNearestThreat(int range)
    {
        Mobile best = null;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < _threats.Count; i++)
        {
            var threat = _threats[i];
            var distance = Mobile.GetDistanceToSqrt(threat);

            if (distance <= range && distance < bestDistance && Mobile.InLOS(threat))
            {
                best = threat;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>A threat whose current target is <paramref name="victim"/>, nearest first.</summary>
    protected Mobile FindThreatAttacking(Mobile victim, int range)
    {
        if (victim == null)
        {
            return null;
        }

        Mobile best = null;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < _threats.Count; i++)
        {
            var threat = _threats[i];

            if (threat.Combatant != victim)
            {
                continue;
            }

            var distance = Mobile.GetDistanceToSqrt(threat);

            if (distance <= range && distance < bestDistance)
            {
                best = threat;
                bestDistance = distance;
            }
        }

        return best;
    }

    // ---------- Combat ----------

    private bool EngageThreats(bool holdPosition)
    {
        var employer = Employer;

        // Don't get dragged off chasing things while the employer walks away. Having a target
        // selected isn't fighting it: someone running from an ogre still has the ogre targeted.
        if (!holdPosition && employer?.Map == Mobile.Map && !Mobile.InRange(employer, LeashRange) &&
            !(employer.Combatant is { } foe && employer.InRange(foe, EmployerFightRange)))
        {
            if (_inCombat)
            {
                EndCombat();
            }

            return false;
        }

        // Just got away: fight whatever followed, but don't go back for the rest.
        if (Party.JustRetreated(Core.TickCount) && employer != null && !IsPursued(employer))
        {
            if (_inCombat)
            {
                EndCombat();
            }

            return false;
        }

        var target = SelectTarget(holdPosition);

        if (target == null)
        {
            return false;
        }

        if (!_inCombat)
        {
            _inCombat = true;

            if (Utility.RandomDouble() < 0.5)
            {
                Companion.SpeakBattleCry(target);
            }
        }

        if (Mobile.Combatant != target)
        {
            Mobile.Combatant = target;
            Note($"took on {target.Name}");
        }

        Mobile.FocusMob = target;
        Action = ActionType.Combat;

        return DoActionCombat();
    }

    /// <summary>
    /// A kill order only sets the combatant in the base AI; switch to combat as well, or a
    /// companion that wasn't already fighting just stands about.
    /// </summary>
    public override bool DoOrderAttack()
    {
        var target = Mobile.ControlTarget;

        if (!Mobile.IsDeadPet && target is { Deleted: false, Alive: true, IsDeadBondedPet: false } &&
            target.Map == Mobile.Map)
        {
            _inCombat = true;
            Mobile.FocusMob = target;
            Action = ActionType.Combat;
        }

        return base.DoOrderAttack();
    }

    /// <summary>Default melee: close in and keep facing the target. Swings happen on their own.</summary>
    public override bool DoActionCombat()
    {
        var combatant = Mobile.Combatant;

        if (!IsValidFocusMob(combatant))
        {
            Mobile.Combatant = null;
            return true;
        }

        if (MoveTo(combatant, true, Mobile.RangeFight))
        {
            Mobile.Direction = Mobile.GetDirectionTo(combatant);
        }

        return true;
    }

    private void EndCombat()
    {
        _inCombat = false;
        _warnedLowHealth = false;
        _warnedEmployerHurt = false;

        Mobile.Combatant = null;
        Mobile.FocusMob = null;
        Mobile.Warmode = false;
        Action = ActionType.Wander;

        if (Mobile.ControlOrder is OrderType.Follow or OrderType.Guard or OrderType.None)
        {
            Mobile.ControlTarget = Employer;
            Mobile.ControlOrder = OrderType.Follow;
        }

        // Getting away isn't winning.
        if (!Party.JustRetreated(Core.TickCount) && Utility.RandomDouble() < 0.4)
        {
            Companion.Speak(SpeechContext.Victory);
        }
    }

    // ---------- Spellcasting ----------

    /// <summary>
    /// Starts casting <paramref name="spell"/> at <paramref name="target"/>. Reagents come out of the
    /// companion's own pack, the same as a player's would.
    /// </summary>
    protected bool CastAt(Spell spell, Mobile target) => BeginCast(spell, target);

    /// <summary>Starts casting a spell aimed at an item in the companion's own pack (Mark on a rune).</summary>
    protected bool CastOnItem(Spell spell, Item target) => BeginCast(spell, target);

    /// <summary>Starts casting a spell aimed at the ground (a field, a teleport).</summary>
    protected bool CastAtLocation(Spell spell, Point3D location) => BeginCast(spell, new LandTarget(location, Mobile.Map));

    /// <summary>Whether the spell is one this companion has learned. NPCs need no book to cast.</summary>
    protected virtual bool KnowsSpell(Spell spell) => true;

    private bool BeginCast(Spell spell, IPoint3D target)
    {
        if (Mobile.Spell != null || Mobile.Target != null || !KnowsSpell(spell) || !HasReagents(spell.Info))
        {
            return false;
        }

        if (!spell.Cast())
        {
            return false;
        }

        Note(target is Mobile m && m != Mobile ? $"cast {spell.Name} on {m.Name}" : $"cast {spell.Name}");

        // Targeted spells pay when the target is chosen, so a disturbed cast costs nothing.
        // Spells without a target (gate travel) pay up front.
        if (target == null)
        {
            ConsumeReagents(spell.Info);
        }
        else
        {
            _spellTarget = target;
            _spellReagents = spell.Info;
        }

        return true;
    }

    private void ConsumeReagents(SpellInfo info)
    {
        if (info?.Reagents.Length > 0)
        {
            Mobile.Backpack?.ConsumeTotal(info.Reagents, info.Amounts);
        }
    }

    protected bool HasReagents(SpellInfo info)
    {
        var pack = Mobile.Backpack;

        if (info.Reagents.Length == 0)
        {
            return true;
        }

        if (pack == null)
        {
            return false;
        }

        for (var i = 0; i < info.Reagents.Length; i++)
        {
            if (pack.GetAmount(info.Reagents[i]) < info.Amounts[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>When a spell finishes casting and asks for a target, aim it at whoever it was meant for.</summary>
    private bool ContinueCasting()
    {
        var target = Mobile.Target;

        if (target == null)
        {
            return false;
        }

        var intended = _spellTarget;
        var reagents = _spellReagents;
        _spellTarget = null;
        _spellReagents = null;

        var stillThere = intended switch
        {
            Mobile m  => !m.Deleted && m.Map == Mobile.Map && Mobile.CanSee(m),
            Item item => !item.Deleted && item.IsChildOf(Mobile.Backpack),
            _         => intended != null
        };

        // An item in our own pack is always in reach; its location is relative to the pack.
        var inReach = intended is Item || target.Range < 0 || Mobile.InRange(intended, target.Range);

        if (stillThere && Mobile.InLOS(intended) && inReach)
        {
            ConsumeReagents(reagents);
            target.Invoke(Mobile, intended);
        }
        else
        {
            target.Cancel(Mobile, TargetCancelType.Canceled);
        }

        return true;
    }

    // ---------- Survival ----------

    private bool TrySurvive()
    {
        var now = Core.TickCount;

        if (now - _nextPotion < 0 || _threats.Count == 0)
        {
            return false;
        }

        // Stat potions are only worth it while not already blessed (the same effect never stacks).
        if (Mobile.Poisoned && Mobile.Hits < Mobile.HitsMax * 0.6 && TryDrink<BaseCurePotion>() ||
            !Mobile.Poisoned && Mobile.Hits < Mobile.HitsMax * 0.35 && TryDrink<BaseHealPotion>() ||
            Mobile.Stam < Mobile.StamMax * 0.25 && TryDrink<BaseRefreshPotion>() ||
            Mobile.GetStatMod("[Magic] Str Buff") == null && TryDrink<BaseStrengthPotion>() ||
            Mobile.GetStatMod("[Magic] Dex Buff") == null && TryDrink<BaseAgilityPotion>())
        {
            _nextPotion = now + 4000;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Drinks the way a player does, rules and all. Potions need a free hand, so a warrior with
    /// sword and shield lowers the shield first; the gear check puts it back a moment later.
    /// </summary>
    private bool TryDrink<T>() where T : BasePotion
    {
        // Their own potions only: looted ones belong to the employer.
        var pack = Mobile.Backpack;
        var potion = pack?.FindItemByType<T>(predicate: p => !Companion.IsLooted(p));

        // Heal potions share a cooldown; don't fiddle with gear if it hasn't passed.
        if (potion == null || potion is BaseHealPotion && !Mobile.CanBeginAction(typeof(BaseHealPotion)))
        {
            return false;
        }

        Item offHand = null;

        if (!BasePotion.HasFreeHand(Mobile))
        {
            offHand = Mobile.FindItemOnLayer(Layer.TwoHanded);
            pack.DropItem(offHand);
        }

        var amount = potion.Amount;
        potion.OnDoubleClick(Mobile);

        if (offHand?.Deleted == false)
        {
            Mobile.EquipItem(offHand);
        }

        if (!potion.Deleted && potion.Amount == amount)
        {
            return false;
        }

        // Toss the empty bottle rather than lugging it around.
        pack.ConsumeTotal(typeof(Bottle), 1);
        return true;
    }

    // ---------- Gates ----------

    /// <summary>
    /// When the party has a gate open, companions follow their employer through it. A companion
    /// on the edge of death goes through on its own.
    /// </summary>
    private bool TryFollowGate()
    {
        var gate = Party.ActiveGate;

        if (gate == null)
        {
            return false;
        }

        if (gate.Deleted)
        {
            Party.ActiveGate = null;
            return false;
        }

        // Only companions near the source gate step through; anyone already across stays put.
        if (gate.Map != Mobile.Map || !Mobile.InRange(gate.GetWorldLocation(), 15))
        {
            return false;
        }

        var employer = Employer;
        var employerWent = employer?.Map == gate.TargetMap && employer.InRange(gate.Target, 4);
        var desperate = Mobile.Hits < Mobile.HitsMax * 0.15 && _threats.Count > 0;

        if (!employerWent && !desperate)
        {
            return false;
        }

        if (!Mobile.InRange(gate.GetWorldLocation(), 1))
        {
            MoveTo(gate, true);
            return true;
        }

        Companion.Speak(SpeechContext.ThroughTheGate);
        Mobile.Combatant = null;
        Mobile.Warmode = false;

        var leadingSteed = Companion.IsLeadingSteed();
        Mobile.MoveToWorld(gate.Target, gate.TargetMap);

        if (leadingSteed)
        {
            Companion.BringSteed();
        }

        Mobile.PlaySound(0x1FE);
        return true;
    }

    // ---------- Loot ----------

    // Below this, a companion comes to the employer to be bandaged before going after corpses.
    private const double LootHealthThreshold = 0.9;

    private bool TryLoot()
    {
        var employer = Employer;

        if (Companion.IsLaden || employer?.Combatant != null || Mobile.Poisoned ||
            Mobile.Hits < Mobile.HitsMax * LootHealthThreshold)
        {
            ReleaseCorpse();
            return false;
        }

        var now = Core.TickCount;

        if (now - _nextLootAction < 0)
        {
            return _lootCorpse != null;
        }

        _nextLootAction = now + 700;

        if (_lootCorpse == null)
        {
            _lootCorpse = Party.ClaimCorpse(Companion, 12);

            if (_lootCorpse == null)
            {
                return false;
            }

            _lootGiveUp = now + LootReachMs;
        }

        var corpse = _lootCorpse;
        var location = corpse.GetWorldLocation();

        // Nobody stays behind picking over a corpse while the employer walks on.
        if (corpse.Deleted || corpse.Map != Mobile.Map || !Mobile.InRange(location, 16) ||
            employer?.Map == Mobile.Map && !employer.InRange(location, LootLeashRange))
        {
            ReleaseCorpse();
            return false;
        }

        if (!Mobile.InRange(location, 1))
        {
            // Across water, up a ledge, behind a fence: give it up rather than walk at it forever.
            if (now - _lootGiveUp >= 0)
            {
                Party.FinishCorpse(corpse);
                _lootCorpse = null;
                return false;
            }

            MoveTo(corpse, false);
            return true;
        }

        _lootGiveUp = now + LootReachMs;

        if (!Companion.LootOneItem(corpse))
        {
            Party.FinishCorpse(corpse);
            _lootCorpse = null;
        }

        return true;
    }

    private const int LootReachMs = 15000;
    private const int LootLeashRange = 12;

    private void ReleaseCorpse()
    {
        if (_lootCorpse != null)
        {
            Party.ReleaseCorpse(_lootCorpse);
            _lootCorpse = null;
        }
    }

    // ---------- Sharing ----------

    private bool TryShare()
    {
        var now = Core.TickCount;

        if (now - _nextShare < 0)
        {
            return false;
        }

        _nextShare = now + 8000;

        var employer = Employer;

        if (employer?.Alive != true || employer.Combatant != null || !Companion.HasShareables())
        {
            return false;
        }

        var bank = IsNearBanker();

        if (bank || Mobile.InRange(employer, 3))
        {
            Companion.ShareLoot(bank);
        }

        if (Companion.IsLaden && !bank)
        {
            Party.WantsTownGate = true;

            if (now - _nextLadenCall >= 0 && Companion.Speak(SpeechContext.Laden))
            {
                _nextLadenCall = now + 180000;
            }
        }

        return false;
    }

    private bool IsNearBanker()
    {
        if (!Mobile.Region.IsPartOf<TownRegion>())
        {
            return false;
        }

        foreach (var _ in Mobile.Map.GetMobilesInRange<Banker>(Mobile.Location, 10))
        {
            return true;
        }

        return false;
    }

    // ---------- Mount ----------

    /// <summary>
    /// Walks over to their own mount and climbs on, the way a player would. Out of combat they'll
    /// fetch it from a short way off; in a fight, only if it's right beside them.
    /// </summary>
    private bool TryMount()
    {
        var steed = Companion.Steed;

        if (Companion.StayOnFoot || !Companion.CanMount(steed) || !Mobile.InRange(steed, _threats.Count > 0 ? 1 : 12))
        {
            return false;
        }

        if (!Mobile.InRange(steed, 1))
        {
            MoveTo(steed, true, 1);
            return true;
        }

        Mobile.Direction = Mobile.GetDirectionTo(steed);
        Companion.MountSteed();
        return true;
    }

    // ---------- Movement ----------

    /// <summary>
    /// Hurries to keep up when the employer pulls ahead, as anyone would. Never steps into a fire,
    /// poison or paralysis field: sidesteps it if a neighbouring step is clear, otherwise waits.
    /// Standing in one already, any step out will do.
    /// </summary>
    public override MoveResult DoMoveImpl(Direction d, bool badStateOk)
    {
        if (!AvoidFields(ref d))
        {
            return MoveResult.Blocked;
        }

        var result = base.DoMoveImpl(d, badStateOk);

        if (result == MoveResult.Success && Mobile.Hits >= Mobile.HitsMax * 0.3 && Employer is { } employer &&
            employer.Map == Mobile.Map && !Mobile.InRange(employer, 3))
        {
            Mobile.CurrentSpeed = Mobile.ActiveSpeed;
        }

        return result;
    }

    /// <summary>
    /// Turns a step that would land in a field to the left or right, keeping the pace. False if
    /// all three are fields. Any step goes when already standing in one.
    /// </summary>
    internal bool AvoidFields(ref Direction d)
    {
        if (IsFieldAt(Mobile.Location) || !IsFieldAt(Step(d)))
        {
            return true;
        }

        var running = d & Direction.Running;
        var left = (Direction)(((int)d - 1) & 7) | running;
        var right = (Direction)(((int)d + 1) & 7) | running;

        if (!IsFieldAt(Step(left)))
        {
            d = left;
            return true;
        }

        if (!IsFieldAt(Step(right)))
        {
            d = right;
            return true;
        }

        return false;
    }

    private Point3D Step(Direction d)
    {
        var p = Mobile.Location;
        Server.Movement.Movement.Offset(d, ref p);
        return p;
    }

    /// <summary>A field that hurts or holds whoever walks into it, friend or foe.</summary>
    protected bool IsFieldAt(Point3D p)
    {
        var map = Mobile.Map;

        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var item in map.GetItemsAt(p))
        {
            if (item is PoisonField or FireFieldItem or ParalyzeField)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Walks or runs towards an item (corpse, gate). Pathfinds around obstacles.</summary>
    protected bool MoveTo(Item item, bool run)
    {
        if (Mobile.InRange(item.GetWorldLocation(), 1))
        {
            Path = null;
            return true;
        }

        if (Path?.Goal != item)
        {
            Path = new PathFollower(Mobile, item) { Mover = DoMoveImpl };
        }

        if (Path.Follow(run, 1))
        {
            Path = null;
            return true;
        }

        return false;
    }

    /// <summary>Keeps <paramref name="minRange"/> tiles away from a threat, drifting towards the employer.</summary>
    protected void KeepDistanceFrom(Mobile threat, int minRange)
    {
        if (threat == null || !Mobile.InRange(threat, minRange - 1))
        {
            return;
        }

        var away = threat.GetDirectionTo(Mobile);

        if (!DoMove(away, true))
        {
            DoMove((Direction)(((int)away + 1) & 7), true);
        }
    }

    // ---------- Healing ----------

    private const int ReviveChannelMs = 6000;

    private const int ReviveReachMs = 20000;

    private BaseCompanion _reviving;
    private long _reviveDone;
    private long _reviveGiveUp;
    private long _nextReviveAttempt;

    /// <summary>Earliest tick at which to offer the dead employer resurrection again.</summary>
    protected long NextEmployerRes { get; set; }

    /// <summary>
    /// The party member most in need of care within <paramref name="range"/>: poison first, then the
    /// lowest health below <paramref name="threshold"/>. The employer is weighted slightly higher.
    /// Patients another healer has already claimed are skipped.
    /// </summary>
    protected Mobile FindPatient(double threshold, int range, bool includeSelf = true)
    {
        Mobile best = null;
        var bestNeed = 0.0;

        Consider(Employer, 1.25);

        foreach (var member in Party.Members)
        {
            if (member != null && (includeSelf || member != Mobile) && !member.IsDeadPet)
            {
                Consider(member, 1.0);
            }
        }

        return best;

        void Consider(Mobile m, double weight)
        {
            if (m?.Alive != true || m.Map != Mobile.Map || !Mobile.InRange(m, range))
            {
                return;
            }

            var ratio = m.Hits / (double)m.HitsMax;

            // Someone else is already on it, unless it's critical: then everyone helps.
            if (ratio > 0.4 && Party.IsHealClaimedByOther(m, Companion))
            {
                return;
            }

            var need = (threshold - ratio + (m.Poisoned ? 0.5 : 0)) * weight;

            if (need > bestNeed)
            {
                best = m;
                bestNeed = need;
            }
        }
    }

    /// <summary>
    /// Bandages whoever needs it most. Walks to them when out of combat; in combat only treats
    /// whoever is within reach. Bandaging runs in the background, so the tick is only spent walking.
    /// </summary>
    protected bool TryBandage(double threshold)
    {
        if (BandageContext.GetContext(Mobile) != null)
        {
            return false;
        }

        var bandage = Mobile.Backpack?.FindItemByType<Bandage>();

        if (bandage == null)
        {
            return false;
        }

        var patient = FindPatient(threshold, Threats.Count > 0 ? Bandage.Range : 10);

        if (patient == null)
        {
            return false;
        }

        if (!Mobile.InRange(patient, Bandage.Range))
        {
            MoveTo(patient, true, Bandage.Range);
            return true;
        }

        if (BandageContext.BeginHeal(Mobile, patient) != null)
        {
            Note(patient == Mobile ? "bandaged myself" : $"bandaged {patient.Name}");
            bandage.Consume();
            Party.ClaimHeal(patient, Companion, 8000);

            if (patient != Mobile && Utility.RandomDouble() < 0.3)
            {
                Companion.Speak(SpeechContext.Healing);
            }
        }

        return false;
    }

    /// <summary>
    /// A skilled healer brings fallen companions back, the same way a player would with bandages,
    /// and offers resurrection to a dead employer. Only when the fighting has stopped.
    /// </summary>
    protected bool TryRevive()
    {
        if (Threats.Count > 0 || Mobile.Skills.Healing.Value < 80.0 || Mobile.Skills.Anatomy.Value < 80.0)
        {
            _reviving = null;
            return false;
        }

        if (_reviving != null)
        {
            return ContinueRevive();
        }

        var employer = Employer;

        if (employer?.Alive == false && employer.Map == Mobile.Map && Mobile.InRange(employer, 10) &&
            Core.TickCount - NextEmployerRes >= 0)
        {
            if (!Mobile.InRange(employer, Bandage.Range))
            {
                MoveTo(employer, true, Bandage.Range);
                return true;
            }

            var bandage = Mobile.Backpack?.FindItemByType<Bandage>();

            // The employer gets a resurrection prompt; don't badger them if they decline.
            if (bandage != null && Core.TickCount - NextEmployerRes >= 0 && BandageContext.GetContext(Mobile) == null &&
                BandageContext.BeginHeal(Mobile, employer) != null)
            {
                bandage.Consume();
                NextEmployerRes = Core.TickCount + 30000;
            }

            return true;
        }

        if (Core.TickCount - _nextReviveAttempt < 0)
        {
            return false;
        }

        foreach (var ally in Party.Members)
        {
            if (ally != Companion && CompanionParty.IsInWorld(ally) && ally.IsDeadPet && ally.Map == Mobile.Map &&
                Mobile.InRange(ally, 10) && Mobile.Backpack?.FindItemByType<Bandage>() is { } bandage)
            {
                bandage.Consume();
                _reviving = ally;
                _reviveDone = Core.TickCount + ReviveChannelMs;
                Party.ClaimHeal(ally, Companion, ReviveChannelMs + 2000);
                _reviveGiveUp = Core.TickCount + ReviveReachMs;
                Mobile.Say($"Hold on, {ally.Name}. I've got you.");
                return true;
            }
        }

        return false;
    }

    private bool ContinueRevive()
    {
        var patient = _reviving;

        if (patient.Deleted || !patient.IsDeadPet || patient.Map != Mobile.Map)
        {
            _reviving = null;
            return false;
        }

        if (!Mobile.InRange(patient, Bandage.Range))
        {
            // Can't get to them; leave it to a healer or the employer.
            if (Core.TickCount - _reviveGiveUp >= 0)
            {
                _reviving = null;
                _nextReviveAttempt = Core.TickCount + ReviveReachMs;
                return false;
            }

            MoveTo(patient, true, Bandage.Range);
            _reviveDone = Core.TickCount + ReviveChannelMs;
            Party.ClaimHeal(patient, Companion, ReviveChannelMs + 2000);
            return true;
        }

        if (Core.TickCount - _reviveDone < 0)
        {
            return true;
        }

        _reviving = null;

        var healing = Mobile.Skills.Healing.Value;
        Mobile.CheckSkill(SkillName.Anatomy, 0.0, 120.0);
        Mobile.CheckSkill(SkillName.Healing, 0.0, 120.0);

        if ((healing - 68.0) / 50.0 > Utility.RandomDouble())
        {
            patient.PlaySound(0x214);
            patient.FixedEffect(0x376A, 10, 16);
            patient.ResurrectPet();
        }
        else
        {
            Mobile.Say("Blast. Let me try again.");
        }

        return true;
    }

    // ---------- Ghosts ----------

    private const int GhostSearchIntervalMs = 3000;
    private const int GhostSearchRange = 20;
    private const int PleaIntervalMs = 120000;
    private const int ResurrectorReachMs = 20000;

    private long _diedAt;
    private long _nextGhostSearch;
    private long _nextPlea;
    private Mobile _resurrector;
    private Mobile _unreachableResurrector;
    private long _resurrectorGiveUp;

    /// <summary>
    /// A ghost follows its employer. Once the fighting has stopped and it has waited long enough,
    /// it goes to a wandering healer if one is nearby, or asks a GM, or reminds its employer.
    /// </summary>
    private bool ObeyAsGhost()
    {
        var now = Core.TickCount;

        if (_diedAt == 0)
        {
            _diedAt = now;
            _resurrector = null;
        }

        // Someone in the party is raising us: hold still so the spell or bandage stays in reach.
        if (Party.IsHealClaimedByOther(Mobile, Companion))
        {
            return true;
        }

        if (_threats.Count > 0 || now - _diedAt < (long)CompanionSettings.GhostPatience.TotalMilliseconds)
        {
            return base.Obey();
        }

        if (now - _nextGhostSearch >= 0)
        {
            _nextGhostSearch = now + GhostSearchIntervalMs;
            var found = FindResurrector();

            if (found != _resurrector)
            {
                _resurrector = found;
                _resurrectorGiveUp = now + ResurrectorReachMs;
            }
        }

        if (_resurrector == null)
        {
            if (now - _nextPlea >= 0 && Companion.Speak(SpeechContext.GhostPlea, urgent: true))
            {
                _nextPlea = now + PleaIntervalMs;
            }

            return base.Obey();
        }

        if (!Mobile.InRange(_resurrector, 2))
        {
            var employer = Employer;

            // Can't get there, or the employer is moving on: go back to following them.
            if (now - _resurrectorGiveUp >= 0 || employer?.Map == Mobile.Map && !employer.InRange(Mobile, 12))
            {
                if (now - _resurrectorGiveUp >= 0)
                {
                    _unreachableResurrector = _resurrector;
                }

                _resurrector = null;
                return base.Obey();
            }

            MoveTo(_resurrector, true, 2);
            return true;
        }

        if (_resurrector is BaseHealer healer)
        {
            ResurrectedBy(healer);
        }
        else if (now - _nextPlea >= 0 && Companion.Speak(SpeechContext.GhostAsksStaff, urgent: true))
        {
            _nextPlea = now + PleaIntervalMs;
            _resurrector.SendMessage(
                0x59,
                $"{Mobile.Name}, a companion of {Employer?.Name}, is asking to be resurrected. Use [res on them."
            );
        }

        return true;
    }

    /// <summary>A free healer within reach who will take us, else a visible GM, else nobody.</summary>
    private Mobile FindResurrector()
    {
        Mobile staff = null;

        foreach (var m in Mobile.Map.GetMobilesInRange(Mobile.Location, GhostSearchRange))
        {
            if (m == _unreachableResurrector)
            {
                continue;
            }

            if (m is BaseHealer { Deleted: false, Alive: true } healer and not PricedHealer && Mobile.InLOS(healer) &&
                healer.CheckResurrect(Mobile))
            {
                return healer;
            }

            if (staff == null && m.AccessLevel >= AccessLevel.GameMaster && m.Alive && Mobile.CanSee(m))
            {
                staff = m;
            }
        }

        return staff;
    }

    private void ResurrectedBy(BaseHealer healer)
    {
        healer.Direction = healer.GetDirectionTo(Mobile);
        healer.Say("Return to the world of the living, child.");

        Mobile.PlaySound(0x1F2);
        Mobile.FixedEffect(0x376A, 10, 16);
        Companion.ResurrectPet();

        _resurrector = null;
    }

    // ---------- Pet orders ----------

    /// <summary>
    /// "Release" (context menu or speech) means dismissal: back to the guild, nothing lost.
    /// Intercepted here because the engine wipes a released pet's name and bond as soon as the
    /// order is set, before the next tick would ever reach <see cref="DoOrderRelease"/>.
    /// </summary>
    public override void OnCurrentOrderChanged()
    {
        if (Mobile.ControlOrder == OrderType.Release && Companion.State == CompanionState.Active)
        {
            Companion.ReturnToGuild(SpeechContext.Dismissed);
            return;
        }

        // Told to fight or guard: the retreat is over.
        if (Mobile.ControlOrder is OrderType.Attack or OrderType.Guard && Party != null)
        {
            Party.EndRetreat(Core.TickCount);
        }

        base.OnCurrentOrderChanged();
    }

    public override bool DoOrderRelease()
    {
        Companion.ReturnToGuild(SpeechContext.Dismissed);
        return true;
    }

    public override bool DoOrderTransfer()
    {
        Mobile.SayTo(Employer, "My contract is with you alone.");
        Mobile.ControlTarget = Employer;
        Mobile.ControlOrder = OrderType.Follow;
        return true;
    }
}
