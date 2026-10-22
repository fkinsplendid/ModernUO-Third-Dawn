using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Companions;
using Server.Engines.Craft;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// A grandmaster smith who mends metal weapons and armour handed to him. He appraises the piece
/// for free, quotes a price, and only sets to work once the customer agrees. Repairs follow the
/// same rules as a GM player smith: the metal can lose a point of durability, and a badly
/// damaged piece may take more than one attempt.
/// </summary>
[SerializationGenerator(0)]
public partial class MasterBlacksmith : BaseVendor
{
    private const double SmithSkill = 100.0;
    private const int BaseFee = 10;
    private const int FeePerPoint = 5;

    private static readonly TimeSpan _inspectDelay = TimeSpan.FromSeconds(2.0);
    private static readonly TimeSpan _answerTimeout = TimeSpan.FromSeconds(60.0);
    private static readonly TimeSpan _hammerInterval = TimeSpan.FromSeconds(1.0);
    private const int HammerStrikes = 3;

    // The piece he is holding and whose it is; saved so nothing is lost across a restart.
    [SerializableField(0, setter: "private")]
    private Item _workpiece;

    [SerializableField(1, setter: "private")]
    private Mobile _customer;

    private JobStage _stage;
    private int _quote;
    private bool _paid;
    private int _strikes;
    private TimerExecutionToken _timerToken;

    private enum JobStage
    {
        None,
        Inspecting,
        AwaitingAnswer,
        Working
    }

    [Constructible]
    public MasterBlacksmith() : base("the master blacksmith")
    {
        SetSkill(SkillName.ArmsLore, 100.0);
        SetSkill(SkillName.Blacksmith, 100.0);
        SetSkill(SkillName.Macing, 80.0, 90.0);
        SetSkill(SkillName.Tactics, 80.0, 90.0);
        SetSkill(SkillName.Parry, 80.0, 90.0);
    }

    protected override List<SBInfo> SBInfos { get; } = [];

    public override bool IsActiveVendor => false;

    public override NpcGuild NpcGuild => NpcGuild.BlacksmithsGuild;

    public override VendorShoeType ShoeType => VendorShoeType.Boots;

    public override void InitSBInfo()
    {
    }

    public override void InitOutfit()
    {
        base.InitOutfit();

        AddItem(new FullApron());
        AddItem(new SmithHammer());
    }

    public override bool HandlesOnSpeech(Mobile from) => from.InRange(Location, 12) || base.HandlesOnSpeech(from);

    public override void OnSpeech(SpeechEventArgs e)
    {
        var from = e.Mobile;

        if (e.Handled || !from.Alive || !from.InRange(Location, 12))
        {
            base.OnSpeech(e);
            return;
        }

        if (_stage == JobStage.AwaitingAnswer && from == Payer)
        {
            var answer = ParseAnswer(e.Speech);

            if (answer != null)
            {
                e.Handled = true;

                if (answer.Value)
                {
                    BeginWork();
                }
                else
                {
                    Tell("As you wish.");
                    FinishJob(RepairOutcome.Declined);
                }

                return;
            }
        }

        var speech = e.Speech.ToLowerInvariant();

        // Someone with companions is giving them an order ("all repair"), not talking to him.
        if (_workpiece == null && from.Player && CompanionSystem.GetParty(from) is not { IsEmpty: false } &&
            (speech.Contains("repair") || speech.Contains("apprais") || speech.Contains("mend")))
        {
            SayTo(from, "Hand me the piece and I'll look it over. The looking's free.");
        }

        base.OnSpeech(e);
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (dropped is not (BaseWeapon or BaseArmor))
        {
            return base.OnDragDrop(from, dropped);
        }

        if (_workpiece != null)
        {
            SayTo(from, from == _customer
                ? "One piece at a time, friend."
                : "I'm seeing to another customer's piece. Give me a moment.");
            return false;
        }

        if (!IsMetalwork(dropped))
        {
            SayTo(from, "That's not metalwork. You'll want a different craftsman for that.");
            return false;
        }

        var pack = GetHoldingPack();
        pack.DropItem(dropped);

        Workpiece = dropped;
        Customer = from;
        _paid = false;
        _stage = JobStage.Inspecting;

        Direction = GetDirectionTo(from);
        Tell("Let's have a look.");

        _timerToken.Cancel();
        Timer.StartTimer(_inspectDelay, Appraise, out _timerToken);
        return true;
    }

    /// <summary>A job in hand, from anyone.</summary>
    public bool IsBusy => _workpiece != null;

    /// <summary>Who settles the bill: a companion's employer pays for their gear.</summary>
    private Mobile Payer => _customer is BaseCompanion { Employer: { } employer } ? employer : _customer;

    // A player hears the smith privately. A companion's errand is talked through aloud, so the
    // employer, who is paying, hears it too.
    private void Tell(string text)
    {
        if (_customer?.Player == true)
        {
            SayTo(_customer, text);
        }
        else
        {
            Say(text);
        }
    }

    private void Appraise()
    {
        if (_workpiece?.Deleted != false || _workpiece is not IDurability durability)
        {
            ClearJob();
            return;
        }

        Tell(Describe(_workpiece));

        if (durability.MaxHitPoints <= 0 || durability.HitPoints >= durability.MaxHitPoints)
        {
            Tell("It's in full repair. Nothing for me to do.");
            FinishJob(RepairOutcome.NotNeeded);
            return;
        }

        if (_workpiece is BaseWeapon { PoisonCharges: > 0 })
        {
            Tell("And there's poison on it. Clean that off before I'll put it near my anvil.");
            FinishJob(RepairOutcome.Refused);
            return;
        }

        if (durability.MaxHitPoints <= 1)
        {
            Tell("It's been mended so often that another repair would break it. Best keep it as it is.");
            FinishJob(RepairOutcome.Refused);
            return;
        }

        var payer = Payer;

        if (payer != _customer && (payer?.Map != Map || !payer.InRange(Location, 12)))
        {
            Tell($"I'll need {payer?.Name ?? "your employer"}'s word on the bill before I start.");
            FinishJob(RepairOutcome.Declined);
            return;
        }

        if (GetRepairChance(durability.HitPoints, durability.MaxHitPoints) < 1.0)
        {
            Tell("It's badly damaged. I may not get it right first time, but there's no charge if I fail.");
        }

        _quote = GetQuote(durability.HitPoints, durability.MaxHitPoints);
        _stage = JobStage.AwaitingAnswer;

        Tell(
            payer == _customer
                ? $"I can mend it for {_quote} gold. Shall I?"
                : $"{payer.Name}, I can mend {_customer.Name}'s {GetNoun(_workpiece)} for {_quote} gold. Shall I?"
        );

        Timer.StartTimer(_answerTimeout, OnAnswerTimeout, out _timerToken);
    }

    private void OnAnswerTimeout()
    {
        if (Payer?.InRange(Location, 12) == true)
        {
            Tell("I'll take that as a no.");
        }

        FinishJob(RepairOutcome.Declined);
    }

    private void BeginWork()
    {
        _timerToken.Cancel();

        var payer = Payer;

        if (!TakePayment(payer, _quote))
        {
            Tell(payer == _customer ? "You haven't the gold for it, I'm afraid." : $"You haven't the gold for it, {payer.Name}.");
            FinishJob(RepairOutcome.Declined);
            return;
        }

        _paid = true;
        _stage = JobStage.Working;

        payer.PlaySound(0x32); // coins
        Tell("Right. Give me a moment.");

        _strikes = 0;
        Timer.StartTimer(_hammerInterval, _hammerInterval, HammerStrikes + 1, Hammer, out _timerToken);
    }

    private void Hammer()
    {
        if (++_strikes <= HammerStrikes)
        {
            if (Body.IsHuman && !Mounted)
            {
                Animate(9, 5, 1, true, false, 0);
            }

            PlaySound(0x2A); // anvil
            return;
        }

        CompleteRepair();
    }

    private void CompleteRepair()
    {
        if (_workpiece?.Deleted != false || _workpiece is not IDurability durability)
        {
            RefundIfPaid();
            ClearJob();
            return;
        }

        var curHits = durability.HitPoints;
        var maxHits = durability.MaxHitPoints;

        // Same rules as Repair.cs for a 100 skill smith: a chance to lose one point of
        // durability, then a difficulty check that a GM only fails on badly damaged pieces.
        if (Utility.Random(100) < GetWeakenChance(curHits, maxHits))
        {
            maxHits = durability.MaxHitPoints -= 1;
            curHits = durability.HitPoints = Math.Max(0, curHits - 1);
        }

        if (GetRepairChance(curHits, maxHits) >= Utility.RandomDouble())
        {
            durability.HitPoints = maxHits;
            Tell("There. Good as I can make it.");
            FinishJob(RepairOutcome.Repaired);
        }
        else
        {
            RefundIfPaid();
            Tell("Blast. I couldn't get it right this time. No charge. Hand it back if you want me to try again.");
            FinishJob(RepairOutcome.Failed);
        }
    }

    private void RefundIfPaid()
    {
        var payer = Payer;

        if (_paid && payer?.Deleted == false)
        {
            if (payer.InRange(Location, 12))
            {
                payer.AddToBackpack(new Gold(_quote));
            }
            else
            {
                Banker.Deposit(payer, _quote);
            }
        }

        _paid = false;
    }

    /// <summary>
    /// Gives the piece back. A player gets it in their backpack if they're still here, otherwise at
    /// their bank. A companion always gets it in their pack, wherever they are; if they're gone for
    /// good it goes to their employer's bank.
    /// </summary>
    private void FinishJob(RepairOutcome outcome)
    {
        _timerToken.Cancel();

        var item = _workpiece;
        var from = _customer;
        var payer = Payer;

        ClearJob();

        if (item?.Deleted != false)
        {
            return;
        }

        if (from is BaseCompanion { Deleted: false, Backpack: { } companionPack } companion)
        {
            companionPack.DropItem(item);
            companion.OnRepairReturned(item, outcome);
            return;
        }

        var owner = from is BaseCompanion ? payer : from;

        if (owner?.Deleted != false)
        {
            item.MoveToWorld(Location, Map);
            return;
        }

        if (owner.Map == Map && owner.InRange(Location, 12) && owner == from)
        {
            SayTo(owner, "Here you are.");
            owner.AddToBackpack(item);
            return;
        }

        owner.BankBox.DropItem(item);
        owner.SendMessage("The master blacksmith has left your item with your banker.");
    }

    private void ClearJob()
    {
        _timerToken.Cancel();
        Workpiece = null;
        Customer = null;
        _stage = JobStage.None;
        _quote = 0;
        _paid = false;
    }

    private Container GetHoldingPack()
    {
        var pack = Backpack;

        if (pack == null)
        {
            pack = new Backpack { Movable = false };
            AddItem(pack);
        }

        return pack;
    }

    private static bool TakePayment(Mobile from, int amount) =>
        from.Backpack?.ConsumeTotal(typeof(Gold), amount) == true || Banker.Withdraw(from, amount);

    private static readonly HashSet<string> _noWords = ["no", "nay", "nope", "back", "keep", "nevermind"];

    private static readonly HashSet<string> _yesWords =
    [
        "yes", "aye", "ok", "okay", "sure", "deal", "agreed", "please", "yep", "yeah", "repair", "mend", "fix"
    ];

    private static bool? ParseAnswer(string speech)
    {
        var words = speech.ToLowerInvariant().Split(
            [' ', ',', '.', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries
        );

        var yes = false;

        foreach (var word in words)
        {
            if (_noWords.Contains(word))
            {
                return false;
            }

            yes |= _yesWords.Contains(word);
        }

        return yes ? true : null;
    }

    /// <summary>Metal weapons and armour a blacksmith could make: the only work he takes.</summary>
    public static bool IsMetalwork(Item item) =>
        DefBlacksmithy.CraftSystem.CraftItems.SearchForSubclass(item.GetType()) != null ||
        item is Pitchfork or RadiantScimitar or WarCleaver or ElvenSpellblade or AssassinSpike or Leafblade
            or RuneBlade or ElvenMachete or OrnateAxe or DiamondMace or Circlet or RoyalCirclet or GemmedCirclet;

    private static int GetQuote(int curHits, int maxHits) => BaseFee + FeePerPoint * (maxHits - curHits);

    private static int GetWeakenChance(int curHits, int maxHits) => 40 + (maxHits - curHits) - (int)(SmithSkill / 10);

    private static double GetRepairChance(int curHits, int maxHits)
    {
        var difficulty = ((maxHits - curHits) * 1250 / Math.Max(maxHits, 1) - 250) * 0.1;
        var minSkill = difficulty - 25.0;
        var maxSkill = difficulty + 25.0;

        if (SmithSkill < minSkill)
        {
            return 0.0;
        }

        return SmithSkill >= maxSkill ? 1.0 : (SmithSkill - minSkill) / (maxSkill - minSkill);
    }

    /// <summary>What a smith would call the piece: "helm", "shield", "axe".</summary>
    public static string GetNoun(Item item) =>
        item switch
        {
            BaseShield => "shield",
            BaseWeapon w => w.Type switch
            {
                WeaponType.Axe      => "axe",
                WeaponType.Slashing => "blade",
                WeaponType.Bashing  => "mace",
                WeaponType.Polearm  => "polearm",
                _                   => "weapon"
            },
            _ => item.Layer switch
            {
                Layer.Helm       => "helm",
                Layer.Neck       => "gorget",
                Layer.Gloves     => "gauntlets",
                Layer.Arms       => "arm guards",
                Layer.Pants      => "leggings",
                Layer.InnerLegs  => "leggings",
                Layer.InnerTorso => "chest piece",
                _                => "armour"
            }
        };

    // What a smith can tell by eye. Hidden magic stays hidden: only the base damage and armour count.
    private static string Describe(Item item)
    {
        var noun = item switch
        {
            BaseShield => "shield",
            BaseWeapon => "weapon",
            _          => "piece of armour"
        };

        var durability = (IDurability)item;
        var quality = item switch
        {
            BaseWeapon { Quality: WeaponQuality.Exceptional } or BaseArmor { Quality: ArmorQuality.Exceptional }
                => "exceptional ",
            BaseWeapon { Quality: WeaponQuality.Low } or BaseArmor { Quality: ArmorQuality.Low } => "poorly made ",
            _ => ""
        };

        var crafter = item switch
        {
            BaseWeapon w => w.Crafter,
            BaseArmor a  => a.Crafter,
            _            => null
        };

        var made = string.IsNullOrEmpty(crafter) ? "" : $", made by {crafter}";
        var strength = item switch
        {
            BaseWeapon w => $"It strikes {GetDamageWord((w.MinDamage + w.MaxDamage) / 2)} blows",
            BaseArmor a  => $"It offers {GetArmorWord(a.BaseArmorRating)} protection",
            _            => ""
        };

        var condition = GetConditionWord(durability.HitPoints, durability.MaxHitPoints);
        var wear = durability.MaxHitPoints > 1 && durability.MaxHitPoints < durability.InitMinHits / 2
            ? " It's been mended many times; the metal's growing tired."
            : "";

        var description = $"{quality}{noun}";
        var article = description[0] is 'a' or 'e' ? "An" : "A";

        return $"{article} {description}{made}. {strength}, and it {condition}.{wear}";
    }

    private static string GetConditionWord(int curHits, int maxHits)
    {
        if (maxHits <= 0)
        {
            return "will never wear out";
        }

        if (curHits >= maxHits)
        {
            return "is in perfect condition";
        }

        return Math.Clamp(curHits * 10 / maxHits, 0, 9) switch
        {
            0     => "is close to falling apart",
            <= 2  => "is badly damaged",
            <= 4  => "is fairly worn",
            <= 6  => "shows some wear",
            <= 8  => "has a few nicks and dents",
            _     => "is in near perfect condition"
        };
    }

    private static string GetDamageWord(int averageDamage) =>
        averageDamage switch
        {
            <= 5  => "light",
            <= 10 => "fair",
            <= 15 => "solid",
            <= 20 => "heavy",
            <= 25 => "fearsome",
            _     => "devastating"
        };

    private static string GetArmorWord(int armorRating) =>
        armorRating switch
        {
            < 10 => "little",
            < 20 => "light",
            < 30 => "fair",
            < 40 => "good",
            _    => "superb"
        };

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        // A job interrupted by a restart: hand the piece straight back.
        if (_workpiece != null)
        {
            Timer.StartTimer(() => FinishJob(RepairOutcome.Declined));
        }
    }

    public override void OnDelete()
    {
        FinishJob(RepairOutcome.Declined);
        base.OnDelete();
    }
}

/// <summary>How a job at the <see cref="MasterBlacksmith"/> ended.</summary>
public enum RepairOutcome
{
    Repaired,
    Failed,
    Declined,
    NotNeeded,
    Refused
}
