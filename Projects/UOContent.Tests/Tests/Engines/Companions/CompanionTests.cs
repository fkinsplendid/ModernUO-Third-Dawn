using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Companions;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class CompanionTests : IDisposable
{
    private readonly Expansion _expansion;

    // Felucca, well away from the coordinates other test classes use.
    private static readonly Point3D Origin = new(5200, 3800, 0);

    public CompanionTests()
    {
        // The shard runs Third Dawn rules; test under them.
        _expansion = Core.Expansion;
        Core.Expansion = Expansion.UOTD;

        CompanionSettings.Configure();

        // The test host has no NPC speed table; creatures need at least the default class.
        NPCSpeeds.RegisterSpeed(
            new NPCSpeeds.SpeedClassEntry { Level = SpeedLevel.Medium, ActiveSpeed = 0.2, PassiveSpeed = 0.4, Types = [] }
        );
    }

    public void Dispose() => Core.Expansion = _expansion;

    private static PlayerMobile CreateEmployer()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.RawStr = 100;
        World.AddEntity(player);

        if (player.Backpack == null)
        {
            player.AddItem(new Backpack());
        }

        player.MoveToWorld(Origin, Map.Felucca);
        return player;
    }

    /// <summary>
    /// The test host has no tiledata, so equipment comes out with an invalid layer.
    /// Give hand items the layers the real client data would.
    /// </summary>
    private static void FixHandLayers(Mobile m)
    {
        foreach (var item in m.Items)
        {
            item.Layer = item switch
            {
                BaseShield => Layer.TwoHanded,
                BaseWeapon => Layer.OneHanded,
                _          => item.Layer
            };
        }
    }

    private static void ServeOneDay(BaseCompanion companion)
    {
        for (var i = 0; i < CompanionSettings.WageDayMinutes; i++)
        {
            companion.OnMinuteElapsed();
        }
    }

    [Fact]
    public void Hiring_AttachesToEmployer_AndIncludesFirstDaysWage()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            Assert.True(companion.BeginService(employer));

            Assert.Equal(CompanionState.Active, companion.State);
            Assert.Same(employer, companion.ControlMaster);
            Assert.True(companion.Controlled);
            Assert.True(companion.IsBonded);
            Assert.Equal(1, employer.Followers);
            Assert.Equal(companion.DailyWage, companion.Purse);
            Assert.Same(companion, CompanionSystem.GetCompanion(employer, CompanionRole.Warrior));
            Assert.Equal(Map.Felucca, companion.Map);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Wages_AreTakenOncePerDay()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            companion.Purse = companion.DailyWage * 2;

            ServeOneDay(companion);

            Assert.Equal(companion.DailyWage, companion.Purse);
            Assert.Equal(CompanionState.Active, companion.State);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Unpaid_ReturnsToGuild_KeepsProgress_AndFreesFollowerSlot()
    {
        var employer = CreateEmployer();
        var companion = new CompanionMage();

        try
        {
            companion.BeginService(employer);
            companion.AwardExperience(CompanionSettings.ExperienceToAdvance(1));
            companion.Purse = 0;

            ServeOneDay(companion);

            Assert.Equal(CompanionState.Resting, companion.State);
            Assert.False(companion.Deleted);
            Assert.Equal(Map.Internal, companion.Map);
            Assert.False(companion.Controlled);
            Assert.Equal(0, employer.Followers);
            Assert.Equal(2, companion.Level);
            Assert.Same(companion, CompanionSystem.GetCompanion(employer, CompanionRole.Mage));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Rehire_FromGuild_RestoresService()
    {
        var employer = CreateEmployer();
        var companion = new CompanionBard();

        try
        {
            companion.BeginService(employer);
            companion.ReturnToGuild(SpeechContext.Dismissed);

            Assert.True(companion.ResumeService());
            Assert.Equal(CompanionState.Active, companion.State);
            Assert.Equal(1, employer.Followers);
            Assert.Equal(Map.Felucca, companion.Map);
            Assert.True(companion.IsBonded);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Logout_WaitsOutOfWorld_AndLoginBringsThemBack()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            companion.WaitForEmployer();

            Assert.Equal(CompanionState.Waiting, companion.State);
            Assert.Equal(Map.Internal, companion.Map);
            Assert.Equal(1, employer.Followers);

            // No wages while waiting.
            var purse = companion.Purse;
            ServeOneDay(companion);
            Assert.Equal(purse, companion.Purse);

            companion.RejoinEmployer();

            Assert.Equal(CompanionState.Active, companion.State);
            Assert.Equal(Map.Felucca, companion.Map);
            Assert.True(companion.InRange(employer, 1));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Experience_LevelsUp_RaisingStatsAndSkills()
    {
        var companion = new CompanionMage();

        try
        {
            var intelligence = companion.RawInt;

            companion.AwardExperience(CompanionSettings.ExperienceToAdvance(1) + CompanionSettings.ExperienceToAdvance(2));

            Assert.Equal(3, companion.Level);
            Assert.True(companion.RawInt > intelligence);
            // Magery is the mage's specialty: +10 over the normal curve.
            Assert.Equal(CompanionSettings.SkillCapFor(3) + 10, companion.Skills.Magery.Cap);
            Assert.True(companion.Skills.Magery.Base >= CompanionSettings.SkillFloorFor(3) + 10);
            Assert.Equal(CompanionSettings.SkillCapFor(3), companion.Skills.EvalInt.Cap);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Experience_StopsAtMaxLevel()
    {
        var companion = new CompanionWarrior();

        try
        {
            companion.AwardExperience(int.MaxValue / 2);

            Assert.Equal(CompanionSettings.MaxLevel, companion.Level);
            Assert.Equal(0, companion.Experience);
            Assert.Equal(100.0, companion.Skills.Swords.Cap);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Warrior_EquipsBetterWeapon_AndReturnsIssuedOneToGuild()
    {
        var companion = new CompanionWarrior();
        var better = new Broadsword { DamageLevel = WeaponDamageLevel.Vanq, Layer = Layer.OneHanded };

        try
        {
            FixHandLayers(companion);
            var old = companion.FindItemOnLayer(Layer.OneHanded);
            companion.PackItem(better);
            companion.CheckGear(Core.TickCount);

            Assert.Same(companion, better.Parent);
            Assert.True(old.Deleted);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Mage_RefusesWeaponsAndMetalArmor_AndKeepsSpellbook()
    {
        var companion = new CompanionMage();
        var plate = new PlateChest { Layer = Layer.InnerTorso };
        var sword = new Broadsword { DamageLevel = WeaponDamageLevel.Vanq, Layer = Layer.OneHanded };
        var leather = new LeatherChest { Layer = Layer.InnerTorso };

        try
        {
            foreach (var item in companion.Items)
            {
                if (item is Spellbook)
                {
                    item.Layer = Layer.OneHanded;
                }
            }

            companion.PackItem(plate);
            companion.PackItem(sword);
            companion.PackItem(leather);
            companion.CheckGear(Core.TickCount);

            Assert.Same(companion.Backpack, plate.Parent);
            Assert.Same(companion.Backpack, sword.Parent);
            Assert.Same(companion, leather.Parent);
            Assert.IsType<Spellbook>(companion.FindItemOnLayer(Layer.OneHanded));
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Bard_RefusesShields()
    {
        var companion = new CompanionBard();
        var shield = new MetalKiteShield { Layer = Layer.TwoHanded };

        try
        {
            FixHandLayers(companion);
            companion.PackItem(shield);
            companion.CheckGear(Core.TickCount);

            Assert.Same(companion.Backpack, shield.Parent);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void ShareLoot_TakesCutForWages_GivesRestToEmployer()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            var purse = companion.Purse;
            var cut = (int)(1000 * CompanionSettings.GoldShare);

            companion.Backpack.DropItem(new Gold(1000));

            Assert.True(companion.ShareLoot(false));
            Assert.Equal(purse + cut, companion.Purse);
            Assert.Equal(1000 - cut, employer.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(0, companion.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void LootedPotions_AreShared_GivenPotionsAreKept()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var looted = new GreaterHealPotion();
        var given = new HealPotion();

        try
        {
            companion.BeginService(employer);

            companion.Backpack.DropItem(looted);
            companion.NoteLooted(looted);
            employer.Backpack.DropItem(given);
            Assert.True(companion.OnDragDrop(employer, given));

            Assert.True(companion.HasShareables());
            companion.ShareLoot(false);

            Assert.Same(employer.Backpack, looted.Parent);
            Assert.Same(companion.Backpack, given.Parent);
            Assert.False(companion.IsLooted(looted));

            // Passed back, the looted potion is theirs to keep.
            Assert.True(companion.OnDragDrop(employer, looted));
            Assert.False(companion.IsLooted(looted));
            Assert.False(companion.HasShareables());
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void PotionsTheyCantUse_AreRefused()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var explosion = new ExplosionPotion();

        try
        {
            companion.BeginService(employer);
            employer.Backpack.DropItem(explosion);

            Assert.False(companion.OnDragDrop(employer, explosion));
            Assert.True(companion.OnDragDrop(employer, new RefreshPotion()));
            Assert.True(companion.OnDragDrop(employer, new StrengthPotion()));
            Assert.False(companion.HasShareables());
        }
        finally
        {
            explosion.Delete();
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void ShareLoot_KeepsOwnSupplies_AndHandsOverValuables()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var diamond = new Diamond();

        try
        {
            companion.BeginService(employer);
            var bandages = companion.Backpack.GetAmount(typeof(Bandage));

            companion.Backpack.DropItem(diamond);
            companion.ShareLoot(false);

            Assert.Same(employer.Backpack, diamond.Parent);
            Assert.Equal(bandages, companion.Backpack.GetAmount(typeof(Bandage)));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void WantsLoot_TakesValuables_SkipsJunk()
    {
        var companion = new CompanionWarrior();
        var gold = new Gold(10);
        var junk = new Shirt();
        var plainDagger = new Dagger();
        var magicDagger = new Dagger { AccuracyLevel = WeaponAccuracyLevel.Supremely };

        try
        {
            FixHandLayers(companion);

            Assert.True(companion.WantsLoot(gold));
            Assert.False(companion.WantsLoot(junk));
            Assert.False(companion.WantsLoot(plainDagger));
            Assert.True(companion.WantsLoot(magicDagger));
        }
        finally
        {
            companion.Delete();
            gold.Delete();
            junk.Delete();
            plainDagger.Delete();
            magicDagger.Delete();
        }
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesState()
    {
        var employer = CreateEmployer();
        var companion = new CompanionBard();

        try
        {
            companion.BeginService(employer);
            companion.AwardExperience(CompanionSettings.ExperienceToAdvance(1) + 50);
            companion.Purse = 1234;
            companion.Quiet = true;
            companion.Personality = CompanionPersonality.Witty;

            var writer = new BufferWriter(true);
            companion.Serialize(writer);

            var copy = new CompanionBard(World.NewMobile);
            copy.Deserialize(new BufferReader(writer.Buffer));
            copy.AIObject?._timer.Stop();

            Assert.Same(employer, copy.Employer);
            Assert.Equal(CompanionState.Active, copy.State);
            Assert.Equal(2, copy.Level);
            Assert.Equal(50, copy.Experience);
            Assert.Equal(1234, copy.Purse);
            Assert.True(copy.Quiet);
            Assert.Equal(CompanionPersonality.Witty, copy.Personality);
            Assert.Equal(companion.Skills.Musicianship.Base, copy.Skills.Musicianship.Base);
            Assert.Equal(companion.RawStr, copy.RawStr);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void ReleaseOrder_SendsToGuild_InsteadOfFreeingThem()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            var name = companion.Name;

            companion.ControlOrder = OrderType.Release;

            Assert.Equal(CompanionState.Resting, companion.State);
            Assert.Equal(name, companion.Name);
            Assert.False(companion.Deleted);
            Assert.Equal(0, employer.Followers);
            Assert.True(companion.ResumeService());
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void IssuedKit_CannotBeTaken_OrShared()
    {
        var employer = CreateEmployer();
        var companion = new CompanionBard();

        try
        {
            companion.BeginService(employer);
            var instrument = companion.Backpack.FindItemByType<BaseInstrument>();
            var given = new Diamond();
            companion.Backpack.DropItem(given);

            Assert.True(companion.IsIssued(instrument));
            Assert.False(companion.CheckNonlocalLift(employer, instrument));
            Assert.True(companion.CheckNonlocalLift(employer, given));

            foreach (var worn in companion.Items)
            {
                Assert.False(companion.CheckNonlocalLift(employer, worn));
            }

            companion.ShareLoot(false);

            Assert.Same(companion.Backpack, instrument.Parent);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void ShareLoot_NoCut_WhenEmployerCannotCarryTheGold()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            employer.RawStr = 1;
            employer.Backpack.DropItem(new Gold(60000)); // already far over their limit
            var purse = companion.Purse;

            companion.Backpack.DropItem(new Gold(1000));

            for (var i = 0; i < 5; i++)
            {
                companion.ShareLoot(false);
            }

            Assert.Equal(purse, companion.Purse);
            Assert.Equal(1000, companion.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void BagOfReagents_StaysWithTheMage()
    {
        var employer = CreateEmployer();
        var companion = new CompanionMage();
        var bag = new Bag();

        try
        {
            companion.BeginService(employer);
            bag.DropItem(new BlackPearl(10));
            bag.DropItem(new Nightshade(10));
            companion.Backpack.DropItem(bag);

            companion.ShareLoot(false);

            Assert.Same(companion.Backpack, bag.Parent);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void EndContract_ReturnsBelongings_ButNotTheGuildKit()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var given = new Diamond();

        try
        {
            companion.BeginService(employer);
            companion.Backpack.DropItem(given);
            companion.ReturnToGuild(SpeechContext.Dismissed);

            var returned = companion.EndContract();

            Assert.True(companion.Deleted);
            Assert.True(returned >= 1);
            Assert.False(given.Deleted);
            Assert.Same(employer.BankBox, given.Parent);
            Assert.Null(CompanionSystem.GetCompanion(employer, CompanionRole.Warrior));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Death_LeavesAGhost_AndRevivalRestoresThem()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            var living = companion.Body;

            companion.Kill();

            Assert.True(companion.IsDeadPet);
            Assert.False(companion.Deleted);
            Assert.True(companion.Body.IsGhost);
            Assert.Equal(0, companion.HueMod);
            Assert.Equal(0x204E, companion.Items[0].ItemID);

            companion.Level = 5;
            Assert.Equal(0, companion.Hits);

            companion.ResurrectPet();

            Assert.False(companion.IsDeadPet);
            Assert.Equal(living, companion.Body);
            Assert.Equal(-1, companion.HueMod);

            foreach (var item in companion.Items)
            {
                Assert.NotEqual(0x204E, item.ItemID);
            }
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Bard_StartsWithItsCraftAheadOfTheCurve()
    {
        var companion = new CompanionBard();

        try
        {
            var floor = CompanionSettings.SkillFloorFor(1);

            Assert.Equal(floor + 20, companion.Skills.Musicianship.Base);
            Assert.Equal(floor + 10, companion.Skills.Peacemaking.Base);
            Assert.Equal(floor, companion.Skills.Fencing.Base);
            Assert.Equal(Math.Min(100.0, CompanionSettings.SkillCapFor(1) + 20), companion.Skills.Musicianship.Cap);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Revival_PutsTheirClothesBackOn()
    {
        var employer = CreateEmployer();
        var companion = new CompanionMage();

        try
        {
            companion.BeginService(employer);
            var robe = companion.FindItemOnLayer(Layer.OuterTorso);
            var hat = companion.FindItemOnLayer(Layer.Helm);

            companion.Kill();

            Assert.Same(companion.Backpack, robe.Parent);

            companion.ResurrectPet();

            Assert.Same(companion, robe.Parent);
            Assert.Same(companion, hat.Parent);
            Assert.Same(robe, companion.FindItemOnLayer(Layer.OuterTorso));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    // ---------- Mounts ----------

    private static T CreatePet<T>(Mobile owner) where T : BaseCreature, new()
    {
        var pet = new T();
        pet.MoveToWorld(owner.Location, owner.Map);
        pet.SetControlMaster(owner);
        pet.ControlTarget = owner;
        pet.ControlOrder = OrderType.Follow;
        return pet;
    }

    private static void Transfer(BaseCreature pet, Mobile to)
    {
        pet.ControlTarget = to;
        pet.ControlOrder = OrderType.Transfer;
        pet.AIObject.DoOrderTransfer();
    }

    [Fact]
    public void Transfer_HandsAHorseToTheCompanion_WhoOwnsAndRidesIt()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            var followers = employer.Followers;

            Transfer(horse, companion);

            Assert.Same(horse, companion.Steed);
            Assert.Same(companion, horse.ControlMaster);
            Assert.Equal(followers - horse.ControlSlots, employer.Followers);
            Assert.Equal(OrderType.Follow, horse.ControlOrder);

            Assert.True(companion.CanMount(horse));
            companion.MountSteed();

            Assert.True(companion.IsRiding);
            Assert.Same(horse, companion.Mount);
            Assert.Equal(Map.Internal, horse.Map);
            Assert.Equal(0.1, companion.ActiveSpeed, 3);
            Assert.Equal(0.2, companion.PassiveSpeed, 3);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Transfer_RefusesPetsThatArentMounts_AndBeastsTheyCantHandle()
    {
        var employer = CreateEmployer();
        employer.FollowersMax = 10;
        var companion = new CompanionMage();
        var wolf = CreatePet<GreyWolf>(employer);
        var nightmare = CreatePet<Nightmare>(employer);

        try
        {
            companion.BeginService(employer);

            Transfer(wolf, companion);
            Transfer(nightmare, companion);

            Assert.Null(companion.Steed);
            Assert.Same(employer, wolf.ControlMaster);
            Assert.Same(employer, nightmare.ControlMaster);
            Assert.Equal(OrderType.Follow, nightmare.ControlOrder);
        }
        finally
        {
            companion.Delete();
            wolf.Delete();
            nightmare.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Dismount_LeavesTheSteedStanding_AndMountUpLetsThemRideAgain()
    {
        var employer = CreateEmployer();
        var companion = new CompanionBard();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.MountSteed();

            Assert.True(companion.Dismount(true));

            Assert.False(companion.Mounted);
            Assert.True(companion.StayOnFoot);
            Assert.Equal(companion.Map, horse.Map);
            Assert.Equal(OrderType.Stay, horse.ControlOrder);
            Assert.Equal(0.2, companion.ActiveSpeed, 3);

            companion.MountUp(true);

            Assert.False(companion.StayOnFoot);
            Assert.Equal(OrderType.Follow, horse.ControlOrder);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Death_ThrowsTheRider_AndTheSteedStaysWithTheirGhost()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.MountSteed();

            companion.Kill();

            Assert.False(horse.Deleted);
            Assert.Null(horse.Rider);
            Assert.Equal(companion.Map, horse.Map);
            Assert.Same(companion, horse.ControlTarget);
            Assert.Same(horse, companion.Steed);
            Assert.False(companion.CanMount(horse));

            companion.ResurrectPet();

            Assert.True(companion.CanMount(horse));
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void KilledSteed_IsForgotten()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);

            horse.Kill();
            companion.CheckSteed(Core.TickCount);

            Assert.Null(companion.Steed);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Logout_TakesTheSteedAlong_AndBringsItBack()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.Dismount(true);

            companion.WaitForEmployer();
            Assert.Equal(Map.Internal, horse.Map);

            companion.RejoinEmployer();
            Assert.Equal(companion.Map, horse.Map);
            Assert.True(horse.InRange(companion, 2));
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void EndContract_GivesTheRiddenSteedBack()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.MountSteed();
            companion.ReturnToGuild(SpeechContext.Dismissed);

            companion.EndContract();

            Assert.False(horse.Deleted);
            Assert.Same(employer, horse.ControlMaster);
            Assert.Equal(employer.Map, horse.Map);
            Assert.True(horse.InRange(employer, 12));
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void EndContract_StablesTheSteed_WhenTheEmployerHasNoRoom()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.MountSteed();
            companion.ReturnToGuild(SpeechContext.Dismissed);
            employer.Followers = employer.FollowersMax;

            companion.EndContract();

            Assert.False(horse.Deleted);
            Assert.True(horse.IsStabled);
            Assert.Contains(horse, employer.Stabled);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void HandBackSteed_ReturnsItToTheEmployer()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.MountSteed();

            Assert.True(companion.HandBackSteed());

            Assert.Null(companion.Steed);
            Assert.False(companion.Mounted);
            Assert.Same(employer, horse.ControlMaster);
            Assert.Equal(companion.Map, horse.Map);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Serialization_RoundTrip_KeepsTheSteed()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.StayOnFoot = true;

            var writer = new BufferWriter(true);
            companion.Serialize(writer);

            var copy = new CompanionWarrior(World.NewMobile);
            copy.Deserialize(new BufferReader(writer.Buffer));
            copy.AIObject?._timer.Stop();

            Assert.Same(horse, copy.Steed);
            Assert.True(copy.StayOnFoot);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    // ---------- Formation ----------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Formation_GivesEveryoneTheirOwnSpace_WhicheverWayTheEmployerHeads(int space)
    {
        var roles = new[] { CompanionRole.Warrior, CompanionRole.Bard, CompanionRole.Mage };

        for (var heading = 0; heading < 8; heading++)
        {
            var spots = new (int X, int Y)[roles.Length];

            for (var i = 0; i < roles.Length; i++)
            {
                spots[i] = CompanionAI.FormationOffset(roles[i], (Direction)heading, space);
                Assert.Equal(space, Math.Max(Math.Abs(spots[i].X), Math.Abs(spots[i].Y)));
            }

            for (var i = 0; i < spots.Length; i++)
            {
                for (var j = i + 1; j < spots.Length; j++)
                {
                    var apart = Math.Max(Math.Abs(spots[i].X - spots[j].X), Math.Abs(spots[i].Y - spots[j].Y));
                    Assert.True(apart >= space, $"{roles[i]} and {roles[j]} are {apart} apart heading {(Direction)heading}");
                }
            }
        }
    }

    [Fact]
    public void Formation_MageAndBardWalkBehind_WarriorAtTheSide()
    {
        // Heading north: behind is south (+Y), right is east (+X).
        Assert.Equal((2, 0), CompanionAI.FormationOffset(CompanionRole.Warrior, Direction.North, 2));
        Assert.Equal((2, 2), CompanionAI.FormationOffset(CompanionRole.Bard, Direction.North, 2));
        Assert.Equal((-2, 2), CompanionAI.FormationOffset(CompanionRole.Mage, Direction.North, 2));
    }

    // ---------- Separation ----------

    [Fact]
    public void Separation_AfterRecallOrTeleport_RejoinsAlmostAtOnce()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            employer.MoveToWorld(new Point3D(Origin.X + 200, Origin.Y, Origin.Z), Map.Felucca);

            var start = Core.TickCount + 60000;
            companion.CheckSeparation(start);
            companion.CheckSeparation(start + 1000);
            Assert.False(companion.InRange(employer, 18));

            companion.CheckSeparation(start + 3500);
            Assert.True(companion.InRange(employer, 1));
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Separation_OnAnotherFacet_RejoinsAlmostAtOnce()
    {
        var employer = CreateEmployer();
        var companion = new CompanionMage();

        try
        {
            companion.BeginService(employer);
            employer.MoveToWorld(new Point3D(1000, 1000, 0), Map.Trammel);

            var start = Core.TickCount + 60000;
            companion.CheckSeparation(start);
            companion.CheckSeparation(start + 3500);

            Assert.Equal(Map.Trammel, companion.Map);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Separation_WhenOutrun_CatchesUpAfterTheDelay_AndBringsTheirMount()
    {
        var employer = CreateEmployer();
        var companion = new CompanionBard();
        var horse = CreatePet<Horse>(employer);

        try
        {
            companion.BeginService(employer);
            Transfer(horse, companion);
            companion.MountSteed();
            employer.MoveToWorld(new Point3D(Origin.X + 25, Origin.Y, Origin.Z), Map.Felucca);

            var start = Core.TickCount + 60000;
            var delay = (long)CompanionSettings.CatchUpDelay.TotalMilliseconds;
            companion.CheckSeparation(start);
            companion.CheckSeparation(start + 3500);
            Assert.False(companion.InRange(employer, 18));

            companion.CheckSeparation(start + delay + 1);
            Assert.True(companion.InRange(employer, 1));
            Assert.True(companion.IsRiding);
        }
        finally
        {
            companion.Delete();
            horse.Delete();
            employer.Delete();
        }
    }

    // ---------- Mage ----------

    [Fact]
    public void Mage_FallsBackToASpellItHasReagentsFor()
    {
        var employer = CreateEmployer();
        var mage = new CompanionMage();
        var target = new Horse();

        try
        {
            mage.BeginService(employer);
            target.MoveToWorld(employer.Location, employer.Map);

            // No mandrake: no mind blast or lightning. No nightshade: no poison.
            mage.Backpack.ConsumeTotal(typeof(MandrakeRoot), mage.Backpack.GetAmount(typeof(MandrakeRoot)));
            mage.Backpack.ConsumeTotal(typeof(Nightshade), mage.Backpack.GetAmount(typeof(Nightshade)));

            var ai = (MageCompanionAI)mage.AIObject;

            for (var i = 0; i < 20; i++)
            {
                Assert.IsType<Server.Spells.Third.FireballSpell>(ai.ChooseAttack(target));
            }

            // Fireball is black pearl alone; magic arrow is sulfurous ash alone.
            mage.Backpack.ConsumeTotal(typeof(BlackPearl), mage.Backpack.GetAmount(typeof(BlackPearl)));

            Assert.IsType<Server.Spells.First.MagicArrowSpell>(ai.ChooseAttack(target));

            mage.Backpack.ConsumeTotal(typeof(SulfurousAsh), mage.Backpack.GetAmount(typeof(SulfurousAsh)));

            Assert.Null(ai.ChooseAttack(target));
        }
        finally
        {
            mage.Delete();
            target.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Mage_IsTrainedInPoisoning_StrongEnoughForRegularPoisonOnHire()
    {
        var mage = new CompanionMage();

        try
        {
            var floor = CompanionSettings.SkillFloorFor(1);

            Assert.Equal(floor + 20, mage.Skills.Poisoning.Base);

            // The poison spell is Regular strength above 130.2 (magery plus poisoning), at close range.
            Assert.True(mage.Skills.Magery.Value + mage.Skills.Poisoning.Value > 130.2);
        }
        finally
        {
            mage.Delete();
        }
    }

    [Fact]
    public void Mage_OnlyPoisonsWhatPoisonWillHurt()
    {
        // The test host skips startup, where the poison levels are registered.
        if (Poison.Lethal == null)
        {
            PoisonImpl.Configure();
        }

        var employer = CreateEmployer();
        var mage = new CompanionMage();
        var horse = new Horse();
        var lich = new Lich();

        try
        {
            mage.BeginService(employer);
            var ai = (MageCompanionAI)mage.AIObject;

            Assert.True(ai.IsPoisonable(horse));
            Assert.False(ai.IsPoisonable(lich));

            horse.Paralyzed = true;
            Assert.False(ai.IsPoisonable(horse));
            horse.Paralyzed = false;

            horse.Poison = Poison.Lesser;
            Assert.False(ai.IsPoisonable(horse));
        }
        finally
        {
            mage.Delete();
            horse.Delete();
            lich.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Mage_WeighsDispelOddsLikeTheSpell()
    {
        var employer = CreateEmployer();
        var mage = new CompanionMage();
        var vortex = new EnergyVortex();

        try
        {
            mage.BeginService(employer);
            var ai = (MageCompanionAI)mage.AIObject;

            mage.Skills.Magery.Base = 62.0;
            Assert.Equal(0.05, ai.DispelChance(vortex), 3);

            mage.Skills.Magery.Base = 100.0;
            Assert.Equal(1.0, ai.DispelChance(vortex), 3);
        }
        finally
        {
            mage.Delete();
            vortex.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Mage_NeverWallsOffTheParty_OrFieldsItself()
    {
        var employer = CreateEmployer();
        var mage = new CompanionMage();
        var warrior = new CompanionWarrior();

        try
        {
            mage.BeginService(employer);
            warrior.BeginService(employer);
            employer.MoveToWorld(new Point3D(Origin.X - 6, Origin.Y, Origin.Z), Map.Felucca);
            mage.MoveToWorld(Origin, Map.Felucca);
            warrior.MoveToWorld(new Point3D(Origin.X + 4, Origin.Y, Origin.Z), Map.Felucca);

            var ai = (MageCompanionAI)mage.AIObject;
            var onWarrior = new Point3D(Origin.X + 4, Origin.Y, Origin.Z);

            // A wall there would trap the warrior; a poison field wouldn't touch them.
            Assert.False(ai.FieldIsClear(onWarrior, 2, 1, true));
            Assert.True(ai.FieldIsClear(onWarrior, 2, -1, true));

            // Nothing at the mage's own feet.
            Assert.False(ai.FieldIsClear(new Point3D(Origin.X + 1, Origin.Y, Origin.Z), 2, -1, true));

            // Clear ground well away from everyone.
            Assert.True(ai.FieldIsClear(new Point3D(Origin.X, Origin.Y + 6, Origin.Z), 2, 1, true));
        }
        finally
        {
            mage.Delete();
            warrior.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void SkillsWindow_CastChance_MatchesThirdDawnSpellOdds()
    {
        var companion = new CompanionMage();

        try
        {
            // Pre-ML, Mark (sixth circle) runs 50-90 and Gate Travel (seventh) 60-100.
            Assert.Equal(0.5, MageCompanionAI.CastChance(new Server.Spells.Sixth.MarkSpell(companion), 70.0), 3);
            Assert.Equal(0.25, MageCompanionAI.CastChance(new Server.Spells.Seventh.GateTravelSpell(companion), 70.0), 3);
            Assert.Equal(0.0, MageCompanionAI.CastChance(new Server.Spells.Seventh.GateTravelSpell(companion), 40.0), 3);
            Assert.Equal(1.0, MageCompanionAI.CastChance(new Server.Spells.Sixth.MarkSpell(companion), 100.0), 3);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Mark_NeedsABlankRune_ThenCastsOnIt()
    {
        var employer = CreateEmployer();
        var companion = new CompanionMage();

        try
        {
            companion.BeginService(employer);

            // The usual test spot is in the dungeon block, where Mark is forbidden. Britain will do.
            companion.MoveToWorld(new Point3D(1450, 1650, 10), Map.Trammel);
            companion.Skills.Magery.Cap = 100.0;
            companion.Skills.Magery.Base = 100.0;
            companion.Mana = companion.ManaMax;
            companion.Backpack.DropItem(new BlackPearl(10));
            companion.Backpack.DropItem(new Bloodmoss(10));
            companion.Backpack.DropItem(new MandrakeRoot(10));

            var ai = (MageCompanionAI)companion.AIObject;

            // No blank rune yet.
            ai.RequestMark(null);
            Assert.Null(companion.Spell);

            companion.Backpack.DropItem(new RecallRune());
            ai.RequestMark(null);
            Assert.IsType<Server.Spells.Sixth.MarkSpell>(companion.Spell);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    // ---------- Gate destinations ----------

    private static readonly Point3D Britain = new(1450, 1650, 10);

    private static RecallRune MarkedRune(string name, Point3D at, Map map)
    {
        var rune = new RecallRune { Marked = true, Target = at, TargetMap = map, Description = name };
        return rune;
    }

    /// <summary>Hands the mage marked runes, which it files into its own runebook.</summary>
    private static void FileRunes(CompanionMage mage, params (string Name, Point3D At)[] places)
    {
        foreach (var (name, at) in places)
        {
            Assert.True(mage.FileRune(MarkedRune(name, at, Map.Trammel)));
        }
    }

    private static CompanionMage HireMageInBritain(PlayerMobile employer)
    {
        var mage = new CompanionMage();
        mage.BeginService(employer);
        mage.MoveToWorld(Britain, Map.Trammel);
        return mage;
    }

    [Fact]
    public void Mage_IsIssuedAnEmptyRunebook_ThatTheEmployerCannotTake()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var book = mage.Runebook;

            Assert.NotNull(book);
            Assert.Empty(book.Entries);
            Assert.Same(mage.Backpack, book.Parent);
            Assert.False(mage.CheckNonlocalLift(employer, book));
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void FiledRunes_GoIntoTheBook_FirstBecomesTheDefault()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var ai = (MageCompanionAI)mage.AIObject;
            var rune = MarkedRune("Minoc Mine Entrance", new Point3D(2500, 500, 0), Map.Trammel);
            mage.Backpack.DropItem(rune);

            Assert.True(mage.FileRune(rune));
            Assert.True(rune.Deleted);

            FileRunes(mage, ("Yew Gate", new Point3D(500, 1000, 0)));

            Assert.Equal(2, mage.Runebook.Entries.Count);
            Assert.True(ai.FindGateDestination(out var place));
            Assert.Equal("Minoc Mine Entrance", place.Name);

            ai.RequestUsual("yew");

            Assert.True(ai.FindGateDestination(out place));
            Assert.Equal("Yew Gate", place.Name);
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void FullRunebook_LeavesTheRuneLoose_AndGatesPreferTheBookDefault()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var ai = (MageCompanionAI)mage.AIObject;

            for (var i = 0; i < CompanionMage.RunebookCapacity; i++)
            {
                FileRunes(mage, ($"Spot {i}", new Point3D(1000 + i, 1000, 0)));
            }

            var extra = MarkedRune("Overflow", new Point3D(1200, 1200, 0), Map.Trammel);
            mage.Backpack.DropItem(extra);

            Assert.False(mage.FileRune(extra));
            Assert.False(extra.Deleted);
            Assert.True(ai.FindGateDestination(out var place));
            Assert.Equal("Spot 0", place.Name);
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Gates_NeverLeadToWhereWeAlreadyStand()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var ai = (MageCompanionAI)mage.AIObject;
            var here = new Point3D(mage.X + 5, mage.Y, mage.Z);

            FileRunes(mage, ("Britain Bank", here), ("Minoc", new Point3D(2510, 560, 0)));

            // The usual place is a few tiles away, so there's nowhere to gate to.
            Assert.False(ai.FindGateDestination(out _));
            Assert.Equal("We're already at Britain Bank.", ai.RequestGate("britain bank"));

            // A loose rune leading here is skipped too.
            mage.Backpack.DropItem(MarkedRune("Next door", here, Map.Trammel));
            Assert.False(ai.FindGateDestination(out _));

            ai.RequestUsual("minoc");
            Assert.True(ai.FindGateDestination(out var place));
            Assert.Equal("Minoc", place.Name);
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void HandedRune_KeepsItsName()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var rune = MarkedRune("brit bank w", new Point3D(1420, 1690, 0), Map.Trammel);
            employer.Backpack.DropItem(rune);
            mage.OnDragDrop(employer, rune);

            Assert.Equal("brit bank w", mage.Runebook.Entries[0].Description);
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Theory]
    [InlineData(" gate to brit bank", "brit bank")]
    [InlineData(" gate brit bank", "brit bank")]
    [InlineData(", gate us to Minoc mine, please", "Minoc mine")]
    [InlineData(" gate to yew gate", "yew gate")]
    [InlineData(" gate to town", "town")]
    [InlineData(" gate", null)]
    [InlineData(" gate please", null)]
    [InlineData(" town", null)]
    public void GatePlace_ReadsTheNameAfterGate(string command, string expected)
    {
        Assert.Equal(expected, CompanionCommands.GatePlace(command));
    }

    [Fact]
    public void Gates_OnlyEverLeadToRunes()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var ai = (MageCompanionAI)mage.AIObject;
            mage.Skills.Magery.Base = 100;

            // Her only rune leads here, so a bare "gate" has nowhere to go.
            FileRunes(mage, ("Britain Bank", new Point3D(mage.X + 5, mage.Y, mage.Z)));

            Assert.False(ai.FindGateDestination(out _));
            Assert.Equal("We're already at Britain Bank. Name somewhere else.", ai.RequestGate(null));
            Assert.Null(mage.Spell);

            // Unknown places are refused, not swapped for a moongate.
            Assert.StartsWith("I don't know a place called", ai.RequestGate("brit west"));
            Assert.Null(mage.Spell);

            // No moongates, not even when asked for "town".
            Assert.Null(ai.FindNamedDestination("town"));

            // Nor for an escape: an empty book means no gate.
            mage.Runebook.Entries.Clear();
            Assert.False(ai.FindGateDestination(out _));
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void RunebookGump_RenamesPlaces_AndPicksTheUsual()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            employer.MoveToWorld(mage.Location, mage.Map);
            FileRunes(mage, ("rune", new Point3D(1312, 1081, 0)), ("rune brit bank", new Point3D(1435, 1699, 2)));

            var book = mage.Runebook;
            var gump = new CompanionRunebookGump(employer, mage, book);

            // Rename the second place; a blank answer leaves the first alone.
            RespondTo(gump, 1, ("", 0), ("Britain west bank", 1));

            Assert.Equal("rune", book.Entries[0].Description);
            Assert.Equal("Britain west bank", book.Entries[1].Description);
            Assert.Same(book.Entries[0], book.Default);

            // The second "make usual" button.
            RespondTo(new CompanionRunebookGump(employer, mage, book), 101, ("Despise", 0), ("Britain west bank", 1));

            Assert.Equal("Despise", book.Entries[0].Description);
            Assert.Same(book.Entries[1], book.Default);
            Assert.Equal("Britain west bank", ((MageCompanionAI)mage.AIObject).FindNamedDestination("west bank")?.Name);
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    private static void RespondTo(BaseGump gump, int button, params (string Text, ushort Id)[] entries)
    {
        var block = new List<byte>();
        var ids = new ushort[entries.Length];
        var ranges = new Range[entries.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            var bytes = System.Text.Encoding.BigEndianUnicode.GetBytes(entries[i].Text);
            ids[i] = entries[i].Id;
            ranges[i] = new Range(block.Count, block.Count + bytes.Length);
            block.AddRange(bytes);
        }

        gump.OnResponse(null, new RelayInfo(button, [], ids, ranges, block.ToArray()));
    }

    [Fact]
    public void GateTo_MatchesRunebookEntriesByName()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var ai = (MageCompanionAI)mage.AIObject;

            FileRunes(
                mage,
                ("Minoc Mine Entrance", new Point3D(2500, 500, 0)),
                ("Minoc", new Point3D(2510, 560, 0)),
                ("Shame dungeon", new Point3D(510, 1560, 0))
            );

            Assert.Equal("Minoc", ai.FindNamedDestination("minoc")?.Name);
            Assert.Equal("Minoc Mine Entrance", ai.FindNamedDestination("minoc mine")?.Name);
            Assert.Equal("Minoc Mine Entrance", ai.FindNamedDestination("mine minoc")?.Name);
            Assert.Equal("Shame dungeon", ai.FindNamedDestination("SHAME")?.Name);
            Assert.Null(ai.FindNamedDestination("despise"));
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Death_DestroysTheRunebook_RevivalIssuesAnEmptyOne()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            FileRunes(mage, ("Minoc", new Point3D(2510, 560, 0)));
            var book = mage.Runebook;

            mage.Kill();

            Assert.True(mage.IsDeadPet);
            Assert.True(book.Deleted);
            Assert.Null(mage.Runebook);
            Assert.False(mage.IsIssued(book));

            mage.ResurrectPet();

            Assert.NotNull(mage.Runebook);
            Assert.NotSame(book, mage.Runebook);
            Assert.Empty(mage.Runebook.Entries);
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Mage_KeepsRunes_WhenSharing()
    {
        var mage = new CompanionMage();
        var warrior = new CompanionWarrior();
        var rune = new RecallRune();

        try
        {
            Assert.True(mage.IsOwnSupply(rune));
            Assert.False(warrior.IsOwnSupply(rune));
        }
        finally
        {
            rune.Delete();
            mage.Delete();
            warrior.Delete();
        }
    }

    [Theory]
    [InlineData("rune", null)]
    [InlineData("this rune", null)]
    [InlineData("here please", null)]
    [InlineData("Minoc mine", "Minoc mine")]
    [InlineData("this rune as Minoc mine", "Minoc mine")]
    [InlineData("the rune, called Yew gate.", "Yew gate")]
    [InlineData("it as the Shame entrance", "Shame entrance")]
    public void MarkName_IgnoresFillerWords(string said, string expected) =>
        Assert.Equal(expected, MageCompanionAI.CleanRuneName(said));

    [Fact]
    public void NameRune_KeepsNamesUniqueInTheBook()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);

        try
        {
            var ai = (MageCompanionAI)mage.AIObject;
            FileRunes(mage, ("Minoc", new Point3D(2510, 560, 0)));

            var rune = MarkedRune(null, new Point3D(2520, 560, 0), Map.Trammel);
            ai.NameRune(rune, "minoc");

            Assert.Equal("minoc 2", rune.Description);
            rune.Delete();
        }
        finally
        {
            mage.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void DescribeWhereabouts_GivesABearingFromTheNearestTown()
    {
        // The test host loads no region data; stand up a town to measure from.
        var town = new Server.Regions.TownRegion(
            "Testville",
            Map.Trammel,
            50,
            new Rectangle3D(new Point3D(2000, 2000, -128), new Point3D(2100, 2100, 128))
        ) { GoLocation = new Point3D(2050, 2050, 0) };
        town.Register();

        try
        {
            Assert.Equal("Testville", MageCompanionAI.DescribeWhereabouts(new Point3D(2050, 2050, 0), Map.Trammel));
            Assert.Equal("Outside Testville", MageCompanionAI.DescribeWhereabouts(new Point3D(2050, 2130, 0), Map.Trammel));
            Assert.Equal("North of Testville", MageCompanionAI.DescribeWhereabouts(new Point3D(2060, 1850, 0), Map.Trammel));
            Assert.Equal("South-east of Testville", MageCompanionAI.DescribeWhereabouts(new Point3D(2200, 2200, 0), Map.Trammel));
        }
        finally
        {
            town.Unregister();
        }
    }

    [Fact]
    public void Companions_StepAroundPoisonFields()
    {
        var employer = CreateEmployer();
        var mage = HireMageInBritain(employer);
        var fields = new System.Collections.Generic.List<Item>();

        void PoisonAt(int dx, int dy)
        {
            var field = new Server.Spells.Fifth.PoisonField(0x3915, Point3D.Zero, mage, Map.Trammel, TimeSpan.FromMinutes(1), 0);
            field.MoveToWorld(new Point3D(Britain.X + dx, Britain.Y + dy, Britain.Z), Map.Trammel);
            fields.Add(field);
        }

        try
        {
            var ai = (CompanionAI)mage.AIObject;
            var d = Direction.East | Direction.Running;

            Assert.True(ai.AvoidFields(ref d));
            Assert.Equal(Direction.East | Direction.Running, d);

            // Poison straight ahead: veer north-east, still running.
            PoisonAt(1, 0);
            d = Direction.East | Direction.Running;
            Assert.True(ai.AvoidFields(ref d));
            Assert.Equal(Direction.Right | Direction.Running, d);

            // North-east too: veer south-east.
            PoisonAt(1, -1);
            d = Direction.East;
            Assert.True(ai.AvoidFields(ref d));
            Assert.Equal(Direction.Down, d);

            // A wall of it: wait.
            PoisonAt(1, 1);
            d = Direction.East;
            Assert.False(ai.AvoidFields(ref d));
            Assert.Equal(MoveResult.Blocked, ai.DoMoveImpl(Direction.East, false));

            // Standing in it: any way out will do.
            mage.Location = new Point3D(Britain.X + 1, Britain.Y, Britain.Z);
            d = Direction.East;
            Assert.True(ai.AvoidFields(ref d));
        }
        finally
        {
            foreach (var field in fields)
            {
                field.Delete();
            }

            mage.Delete();
            employer.Delete();
        }
    }
}
