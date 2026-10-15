using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.Companions;

/// <summary>
/// A hired adventurer who follows their employer, fights beside them, heals, loots and levels up.
/// Companions are never deleted for lack of pay or by dying: unpaid or dismissed companions return
/// to the guild with their progress intact, and the dead walk beside their employer until revived.
/// </summary>
[SerializationGenerator(3)]
public abstract partial class BaseCompanion : BaseCreature
{
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _employer;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private CompanionState _state;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private CompanionPersonality _personality;

    private int _level;

    [SerializableField(4, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _experience;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _purse;

    [SerializableField(6, setter: "private")]
    private int _minutesServed;

    [SerializableField(7, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _kills;

    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _quiet;

    // The guild's starting kit. It stays with the companion: it can't be taken off them, isn't
    // shared, and goes back to the guild when replaced or when the contract ends.
    [Tidy]
    [SerializableField(9, setter: "private")]
    private List<Item> _issuedGear;

    // What they had on when they died. Death moves it all into the pack; revival puts it back on.
    [Tidy]
    [SerializableField(10, setter: "private")]
    private List<Item> _wornAtDeath;

    // A mount the employer gave them. They own it, ride it, feed it, and grieve when it's killed.
    [SerializableField(11, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private BaseMount _steed;

    // Told to dismount: they leave the steed standing until told to mount up again.
    [SerializableField(12)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _stayOnFoot;

    // Potions picked up off corpses. They're loot, handed over when sharing; any other potion in
    // the pack came from the employer or the guild, and is theirs to drink.
    [Tidy]
    [SerializableField(13, setter: "private")]
    private List<Item> _lootedPotions;

    private BaseAI _ai;
    private long _separatedSince;
    private bool _purseWarned;

    protected BaseCompanion() : base(AIType.AI_Melee, FightMode.Aggressor, 12)
    {
        _level = 1;
        _personality = (CompanionPersonality)Utility.Random(4);

        Female = Utility.RandomBool();
        Body = LivingBody;
        Name = NameList.RandomName(Female ? "female" : "male");
        Hue = Race.Human.RandomSkinHue();
        Title = Profile.Title;

        Utility.AssignRandomHair(this);

        if (!Female)
        {
            Utility.AssignRandomFacialHair(this, HairHue);
        }

        ControlSlots = 1;
        ActiveSpeed = FootActiveSpeed;
        PassiveSpeed = FootPassiveSpeed;
        CurrentSpeed = FootPassiveSpeed;
        SkillsCap = 10000;

        ApplyLevel(true);
        DressForRole();
    }

    public abstract CompanionRole Role { get; }

    public abstract CompanionProfile Profile { get; }

    public CompanionParty Party { get; internal set; }

    [SerializableProperty(3, useField: nameof(_level))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int Level
    {
        get => _level;
        set
        {
            _level = Math.Clamp(value, 1, CompanionSettings.MaxLevel);
            _experience = 0;
            ApplyLevel(true);
            this.MarkDirty();
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public int DailyWage => CompanionSettings.DailyWageFor(Level);

    public int MinutesUntilWage => CompanionSettings.WageDayMinutes - MinutesServed;

    public int ExperienceToNextLevel => Level >= CompanionSettings.MaxLevel ? 0 : CompanionSettings.ExperienceToAdvance(Level);

    public bool IsWorking => CompanionParty.IsInWorld(this);

    public override bool IsBondable => false;
    public override bool CanDrop => false;
    public override bool KeepsItemsOnDeath => true;
    public override bool DeleteOnRelease => false;
    public override bool InitialInnocent => true;
    public override bool ShowFameTitle => false;
    public override bool AlwaysMurderer => false;
    public override bool CanRummageCorpses => false;
    public override bool CanBeDistracted => false;

    // Bonded pets left alone are normally deleted after a day. Companions are only ever
    // tucked away (stabled flag) or walking beside their employer, but belt and braces.
    public override TimeSpan BondingAbandonDelay => TimeSpan.FromDays(3650);

    /// <summary>
    /// One AI instance for the companion's lifetime. <see cref="BaseCreature.ChangeAIType"/> stops the
    /// current AI's timer before asking for this, so when the cached instance is handed back we
    /// restart it on the next tick.
    /// </summary>
    protected override BaseAI ForcedAI
    {
        get
        {
            if (_ai == null)
            {
                _ai = CreateAI();
            }
            else if (!World.Loading)
            {
                Timer.StartTimer(ReactivateAI);
            }

            return _ai;
        }
    }

    private void ReactivateAI()
    {
        if (!Deleted && Map != null && Map != Map.Internal)
        {
            _ai?.Activate();
        }
    }

    protected abstract BaseAI CreateAI();

    /// <summary>Starting outfit and supplies for a fresh hire.</summary>
    protected abstract void DressForRole();

    public override void GenerateLoot()
    {
    }

    // ---------- Employment ----------

    /// <summary>Starts service with a new employer. The first day's wage is included in the hire fee.</summary>
    public bool BeginService(Mobile employer)
    {
        Employer = employer;

        if (!AttachToEmployer())
        {
            Employer = null;
            return false;
        }

        State = CompanionState.Active;
        Purse += DailyWage;
        MinutesServed = 0;

        CompanionSystem.Register(this);
        MoveToWorld(employer.Location, employer.Map);
        Speak(SpeechContext.Hired, urgent: true);
        return true;
    }

    /// <summary>Brings a companion back from the guild. Their old employer pays one day up front.</summary>
    public bool ResumeService()
    {
        var employer = Employer;

        if (employer?.Deleted != false || State != CompanionState.Resting || !AttachToEmployer())
        {
            return false;
        }

        State = CompanionState.Active;
        Purse += DailyWage;
        MinutesServed = 0;
        _purseWarned = false;

        MoveToWorld(employer.Location, employer.Map);
        BringSteed();
        Speak(SpeechContext.Rehired, urgent: true);
        return true;
    }

    private bool AttachToEmployer()
    {
        var employer = Employer;

        if (employer == null)
        {
            return false;
        }

        if (ControlMaster != employer && !SetControlMaster(employer))
        {
            return false;
        }

        IsStabled = false;
        IsBonded = true;
        Loyalty = MaxLoyalty;
        ControlTarget = employer;
        ControlOrder = OrderType.Follow;
        return true;
    }

    /// <summary>Goes back to the guild hall. Nothing is lost; the employer can rehire them.</summary>
    public void ReturnToGuild(SpeechContext farewell)
    {
        if (State == CompanionState.Resting)
        {
            return;
        }

        if (State == CompanionState.Active)
        {
            Speak(farewell, urgent: true);
        }

        State = CompanionState.Resting;
        Combatant = null;
        Warmode = false;
        ControlTarget = null;
        ControlOrder = OrderType.Stay;
        SetControlMaster(null);
        IsBonded = false;
        IsStabled = true;
        StowSteed();
        Internalize();
    }

    /// <summary>Employer logged out. Wait out of the world until they return.</summary>
    internal void WaitForEmployer()
    {
        if (State != CompanionState.Active)
        {
            return;
        }

        State = CompanionState.Waiting;
        Combatant = null;
        Warmode = false;
        IsStabled = true;
        StowSteed();
        Internalize();
    }

    /// <summary>Employer logged in. Also recovers a companion left in the world by a crash.</summary>
    internal void RejoinEmployer()
    {
        var employer = Employer;

        if (State == CompanionState.Resting || employer?.Deleted != false || employer.Map == null || employer.Map == Map.Internal)
        {
            return;
        }

        State = CompanionState.Active;
        IsStabled = false;
        ControlTarget = employer;
        ControlOrder = OrderType.Follow;
        _separatedSince = 0;

        MoveToWorld(employer.Location, employer.Map);
        BringSteed();
        Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(2, 6)), GreetEmployer);
    }

    private void GreetEmployer() => Speak(SpeechContext.Greeting);

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        if (Employer?.Deleted != false)
        {
            Delete();
            return;
        }

        CompanionSystem.Register(this);

        BecomeGhost(IsDeadPet);
        UpdatePace();

        // Balance changes to stats and skills reach companions hired before them.
        ApplyLevel(false);

        // Nobody is online during a load; whoever was out in the world waits for their employer.
        if (State == CompanionState.Active)
        {
            WaitForEmployer();
        }
    }

    /// <summary>Called once a minute by <see cref="CompanionSystem"/>. Only working time is billed.</summary>
    internal void OnMinuteElapsed()
    {
        if (Deleted)
        {
            return;
        }

        if (Employer?.Deleted != false)
        {
            Delete();
            return;
        }

        if (State != CompanionState.Active)
        {
            return;
        }

        Loyalty = MaxLoyalty;
        MinutesServed++;

        var wage = DailyWage;

        if (MinutesServed < CompanionSettings.WageDayMinutes)
        {
            if (!_purseWarned && Purse < wage && MinutesUntilWage <= 5)
            {
                _purseWarned = true;
                Speak(SpeechContext.WagesDue, MinutesUntilWage.ToString(), urgent: true);
            }

            return;
        }

        MinutesServed = 0;

        if (Purse < wage)
        {
            ReturnToGuild(SpeechContext.LeavingUnpaid);
            return;
        }

        Purse -= wage;
        _purseWarned = false;
    }

    // ---------- Progression ----------

    public void OnPartyKill(BaseCreature victim, int experience)
    {
        Kills++;
        AwardExperience(experience);

        if (victim.LastKiller == this)
        {
            Speak(SpeechContext.Kill, victim.Name);
        }
    }

    public void AwardExperience(int amount)
    {
        var maxLevel = CompanionSettings.MaxLevel;

        if (amount <= 0 || Level >= maxLevel)
        {
            return;
        }

        var level = Level;
        var experience = Experience + amount;

        while (level < maxLevel && experience >= CompanionSettings.ExperienceToAdvance(level))
        {
            experience -= CompanionSettings.ExperienceToAdvance(level);
            level++;
        }

        var leveled = level != Level;
        _level = level;
        Experience = level >= maxLevel ? 0 : experience;

        if (leveled)
        {
            ApplyLevel(false);
            OnLevelUp();
        }
    }

    private void OnLevelUp()
    {
        FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
        PlaySound(0x1F2);
        Speak(SpeechContext.LevelUp, Level.ToString(), urgent: true);
        Employer?.SendMessage(0x59, $"{Name} has reached level {Level}.");
    }

    /// <summary>Raises stats, skill caps and skill floors to match the current level. Never lowers anything.</summary>
    private void ApplyLevel(bool fullHeal)
    {
        var profile = Profile;
        var level = Level;
        var hitsRatio = HitsMax > 0 ? Hits / (double)HitsMax : 1.0;

        RawStr = Math.Max(RawStr, profile.Str.At(level));
        RawDex = Math.Max(RawDex, profile.Dex.At(level));
        RawInt = Math.Max(RawInt, profile.Int.At(level));
        HitsMaxSeed = RawStr + profile.BonusHitsPerLevel * level;

        foreach (var (name, bonus) in profile.Skills)
        {
            var skill = Skills[name];
            var cap = Math.Min(100.0, CompanionSettings.SkillCapFor(level) + bonus);
            var floor = Math.Min(cap, CompanionSettings.SkillFloorFor(level) + bonus);

            skill.Cap = cap;

            if (skill.Base < floor)
            {
                skill.Base = floor;
            }
        }

        // The dead stay dead; a level-up doesn't heal a ghost.
        if (IsDeadPet)
        {
            Hits = 0;
        }
        else if (fullHeal)
        {
            Hits = HitsMax;
            Stam = StamMax;
            Mana = ManaMax;
        }
        else
        {
            Hits = Math.Max(1, (int)(HitsMax * hitsRatio));
        }

        this.MarkDirty();
    }

    /// <summary>Marks an item as part of the guild's starting kit.</summary>
    protected T Issue<T>(T item) where T : Item
    {
        (_issuedGear ??= []).Add(item);
        this.MarkDirty();
        return item;
    }

    public bool IsIssued(Item item) => _issuedGear?.Contains(item) == true;

    /// <summary>Destroys a piece of the guild's kit, taking it off the list.</summary>
    protected void DestroyIssued(Item item)
    {
        _issuedGear?.Remove(item);
        item.Delete();
        this.MarkDirty();
    }

    /// <summary>
    /// Ends the contract for good. Everything the companion owns that the guild didn't issue,
    /// worn or carried, goes to the employer's bank box (or their pack if the bank is full).
    /// </summary>
    public int EndContract()
    {
        var employer = Employer;
        var moved = 0;

        // The steed goes back first: while ridden it is worn, and deleting the rider would take it too.
        ReturnSteed(true);

        using var belongings = PooledRefList<Item>.Create();

        foreach (var item in Items)
        {
            if (item != Backpack && item is not IMountItem && !IsIssued(item))
            {
                belongings.Add(item);
            }
        }

        if (Backpack is { } pack)
        {
            foreach (var item in pack.Items)
            {
                if (!IsIssued(item))
                {
                    belongings.Add(item);
                }
            }
        }

        for (var i = 0; i < belongings.Count; i++)
        {
            var item = belongings[i];

            if (employer?.BankBox.TryDropItem(employer, item, false) == true ||
                employer?.Backpack?.TryDropItem(employer, item, false) == true)
            {
                moved++;
            }
            else if (employer != null)
            {
                item.MoveToWorld(employer.Location, employer.Map);
                moved++;
            }
        }

        Delete();
        return moved;
    }

    // ---------- World behaviour ----------

    public override void OnThink()
    {
        base.OnThink();

        if (State != CompanionState.Active || Deleted)
        {
            return;
        }

        UpdatePace();

        if (!IsDeadPet)
        {
            ThinkUpkeep();
        }
    }

    private const int SeparationRange = 18;
    private const int TravelledRange = 40;
    private const long TravelledCatchUpMs = 3000;

    /// <summary>
    /// Brings back a companion who has lost their employer. Called by <see cref="CompanionSystem"/>
    /// every few seconds rather than from the companion's own AI, because an NPC left behind with no
    /// player nearby has its AI put to sleep and would otherwise stand there until someone came back.
    ///
    /// When the employer has travelled (recall, a gate, a teleporter, another facet) the companion
    /// would have gone along, so they rejoin almost at once. Otherwise, having been outrun or stuck
    /// behind something, they turn up a little later, unless they're busy fighting where they are.
    /// </summary>
    internal void CheckSeparation() => CheckSeparation(Core.TickCount);

    internal void CheckSeparation(long now)
    {
        var employer = Employer;

        if (Deleted || State != CompanionState.Active || employer?.Map == null || employer.Map == Map.Internal)
        {
            return;
        }

        if (employer.Map == Map && InRange(employer, SeparationRange))
        {
            _separatedSince = 0;
            return;
        }

        if (_separatedSince == 0)
        {
            _separatedSince = now;
            return;
        }

        var travelled = employer.Map != Map || !InRange(employer, TravelledRange);
        var delay = travelled ? TravelledCatchUpMs : (long)CompanionSettings.CatchUpDelay.TotalMilliseconds;

        if (now - _separatedSince < delay || IsFightingHere())
        {
            return;
        }

        _separatedSince = 0;
        Combatant = null;
        Warmode = false;

        var leadingSteed = IsLeadingSteed();
        MoveToWorld(employer.Location, employer.Map);

        if (leadingSteed)
        {
            BringSteed();
        }

        employer.SendMessage(0x59, $"{Name} catches up with you.");
    }

    /// <summary>In a fight that is actually going on: a sleeping AI's combatant doesn't count.</summary>
    private bool IsFightingHere() =>
        Combatant is { Deleted: false, Alive: true } foe && foe.Map == Map && InRange(foe, 12) &&
        Map.GetSector(Location).Active;

    private int LivingBody => Female ? 0x191 : 0x190;

    private int GhostBody => Female ? 0x193 : 0x192;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (!IsDeadPet)
        {
            return;
        }

        BecomeGhost(true);
        OnRiderDied();

        if (Party == null)
        {
            return;
        }

        foreach (var ally in Party.Members)
        {
            if (ally != this && CompanionParty.IsFighting(ally))
            {
                ally.Speak(SpeechContext.AllyDied, Name, urgent: true);
                break;
            }
        }
    }

    /// <summary>
    /// The engine shows a dead pet as its own body with the gear hidden, which on a person just
    /// looks naked. A companion takes the ghost body instead. The skin hue has to go too: on the
    /// ghost body it tints the whole figure, and a skin-coloured ghost looks like a naked person.
    /// </summary>
    private void BecomeGhost(bool ghost)
    {
        Body = ghost ? GhostBody : LivingBody;
        HueMod = ghost ? 0 : -1;

        RemoveDeathShroud();

        if (ghost)
        {
            // The same shroud a dead player wears. First in the list, so it wins the robe slot
            // over a mage's own robe when the client is sent the paperdoll.
            var shroud = Issue(new Item(DeathShroudId) { Movable = false, Layer = Layer.OuterTorso });
            AddItem(shroud);
            Items.Remove(shroud);
            Items.Insert(0, shroud);
        }
    }

    private const int DeathShroudId = 0x204E;

    private void RemoveDeathShroud()
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i] is { ItemID: DeathShroudId, Movable: false } shroud && shroud.GetType() == typeof(Item))
            {
                shroud.Delete();
            }
        }
    }

    public override void OnBeforeResurrect()
    {
        BecomeGhost(false);
        base.OnBeforeResurrect();
    }

    public override bool OnBeforeDeath()
    {
        if (!base.OnBeforeDeath())
        {
            return false;
        }

        if (IsBonded)
        {
            RememberOutfit();
        }

        return true;
    }

    private void RememberOutfit()
    {
        _wornAtDeath ??= [];
        _wornAtDeath.Clear();

        foreach (var item in Items)
        {
            if (item != Backpack && item.Movable)
            {
                _wornAtDeath.Add(item);
            }
        }

        this.MarkDirty();
    }

    /// <summary>
    /// Puts back on whatever death moved into the pack. Guild-issued clothing whose slot is empty
    /// goes on too, which also dresses anyone who died before outfits were remembered.
    /// </summary>
    private void Redress()
    {
        var pack = Backpack;

        if (pack == null)
        {
            return;
        }

        using var outfit = PooledRefList<Item>.Create();

        if (_wornAtDeath != null)
        {
            foreach (var item in _wornAtDeath)
            {
                outfit.Add(item);
            }
        }

        foreach (var item in pack.Items)
        {
            if (IsIssued(item) && !outfit.Contains(item))
            {
                outfit.Add(item);
            }
        }

        for (var i = 0; i < outfit.Count; i++)
        {
            var item = outfit[i];

            if (!item.Deleted && item.Parent == pack && item.Layer is not (Layer.Invalid or Layer.Backpack) &&
                FindItemOnLayer(item.Layer) == null)
            {
                EquipItem(item);
            }
        }

        _wornAtDeath?.Clear();
        GearDirty = true;
        this.MarkDirty();
    }

    public override void OnAfterResurrect()
    {
        Redress();
        base.OnAfterResurrect();
        Speak(SpeechContext.Revived, urgent: true);
    }

    public override void OnDelete()
    {
        // Ridden or not, the steed outlives its rider: it goes back to the employer.
        ReturnSteed(true);
        base.OnDelete();
    }

    public override void OnAfterDelete()
    {
        CompanionSystem.Unregister(this);
        _employer = null;
        _steed = null;
        _ai = null;
        base.OnAfterDelete();
    }

    // ---------- Interaction ----------

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from != Employer || State != CompanionState.Active)
        {
            if (from.Player)
            {
                SayTo(from, "I only take coin and orders from my employer.");
            }

            return false;
        }

        if (IsDeadPet)
        {
            return false;
        }

        if (dropped is Gold or BankCheck)
        {
            var amount = dropped is BankCheck check ? check.Worth : dropped.Amount;
            Purse += amount;
            _purseWarned = false;
            dropped.Delete();
            Speak(SpeechContext.Paid, amount.ToString(), urgent: true);
            return true;
        }

        if (dropped is BasePotion potion && !Drinks(potion))
        {
            SayTo(from, "I've no use for that potion. Better you keep it.");
            return false;
        }

        var pack = Backpack;

        if (pack?.TryDropItem(this, dropped, false) != true)
        {
            SayTo(from, "I cannot carry any more than this.");
            return false;
        }

        // Handed back, a looted potion is theirs to keep.
        ForgetLooted(dropped);

        GearDirty = true;
        Speak(SpeechContext.ReceivedItem, urgent: true);
        return true;
    }

    public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) => HasPackAccess(from);

    /// <summary>The employer may take things from the pack, but not what is worn or guild-issued.</summary>
    public override bool CheckNonlocalLift(Mobile from, Item item) =>
        from.AccessLevel >= AccessLevel.GameMaster || HasPackAccess(from) && item.Parent != this && !IsIssued(item);

    public override bool IsSnoop(Mobile from) => !HasPackAccess(from) && base.IsSnoop(from);

    public bool HasPackAccess(Mobile from) =>
        from.AccessLevel >= AccessLevel.GameMaster || from.Alive && from == Employer && !IsDeadPet &&
        State == CompanionState.Active;

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (HasPackAccess(from))
        {
            list.Add(new PackAnimalBackpackEntry(true));
        }
    }

    public override void OnSingleClick(Mobile from)
    {
        base.OnSingleClick(from);

        var label = IsDeadPet ? "(ghost)" : $"(level {Level})";
        PrivateOverheadMessage(MessageType.Label, 0x3B2, false, label, from.NetState);
    }

    public override bool HandlesOnSpeech(Mobile from) =>
        from == Employer && State == CompanionState.Active || base.HandlesOnSpeech(from);

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Handled && e.Mobile == Employer && State == CompanionState.Active &&
            CompanionCommands.TryHandle(this, e.Speech))
        {
            e.Handled = true;
            return;
        }

        base.OnSpeech(e);
    }
}
