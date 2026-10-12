using System;
using Server;
using Server.Engines.Companions;
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
            Assert.Equal(CompanionSettings.SkillCapFor(3), companion.Skills.Magery.Cap);
            Assert.True(companion.Skills.Magery.Base >= CompanionSettings.SkillFloorFor(3));
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
}
