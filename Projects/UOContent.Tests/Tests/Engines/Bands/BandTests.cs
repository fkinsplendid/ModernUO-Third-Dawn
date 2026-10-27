using System;
using Server;
using Server.Engines.Bands;
using Server.Engines.Companions;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class BandTests : IDisposable
{
    private readonly Expansion _expansion;

    // Felucca, away from the coordinates the companion tests use.
    private static readonly Point3D Origin = new(5280, 3880, 0);

    public BandTests()
    {
        _expansion = Core.Expansion;
        Core.Expansion = Expansion.UOTD;

        CompanionSettings.Configure();
        BandSystem.Configure();
        Server.Spells.Initializer.Configure();

        NPCSpeeds.RegisterSpeed(
            new NPCSpeeds.SpeedClassEntry { Level = SpeedLevel.Medium, ActiveSpeed = 0.2, PassiveSpeed = 0.4, Types = [] }
        );
    }

    public void Dispose() => Core.Expansion = _expansion;

    private static void Run(Action<AdventurerBand, BandCaptain> test)
    {
        var band = new AdventurerBand { Level = 12 };
        band.MoveToWorld(Origin, Map.Felucca);

        try
        {
            band.Muster();
            test(band, band.Captain);
        }
        finally
        {
            band.Delete();
        }
    }

    private static PlayerMobile CreatePlayer()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        World.AddEntity(player);
        player.MoveToWorld(Origin, Map.Felucca);
        return player;
    }

    private static T Place<T>(T creature) where T : Mobile
    {
        creature.MoveToWorld(Origin, Map.Felucca);
        return creature;
    }

    [Fact]
    public void Muster_RaisesACaptainWithAWarriorBardAndMage()
    {
        Run((band, captain) =>
        {
            Assert.NotNull(captain);
            Assert.Same(band, captain.Band);

            var party = band.Members;
            Assert.NotNull(party);

            foreach (var role in new[] { CompanionRole.Warrior, CompanionRole.Bard, CompanionRole.Mage })
            {
                var member = party[role];

                Assert.NotNull(member);
                Assert.True(member.InBand);
                Assert.Same(captain, member.Employer);
                Assert.Equal(CompanionState.Active, member.State);
                Assert.Equal(BandSystem.Team, member.Team);
                Assert.Equal(12, member.Level);
            }
        });
    }

    [Fact]
    public void MonstersAndBands_AreFoes()
    {
        Run((band, captain) =>
        {
            var zombie = Place(new Zombie());
            var warrior = band.Members[CompanionRole.Warrior];

            try
            {
                Assert.True(zombie.IsEnemy(captain));
                Assert.True(zombie.IsEnemy(warrior));
                Assert.True(captain.IsEnemy(zombie));
                Assert.False(captain.IsEnemy(warrior));
            }
            finally
            {
                zombie.Delete();
            }
        });
    }

    [Fact]
    public void Captain_NeverTurnsOnPlayersOrTheirPets()
    {
        Run((_, captain) =>
        {
            var player = CreatePlayer();
            var pet = Place(new Horse());

            try
            {
                player.Karma = -5000;
                pet.SetControlMaster(player);

                Assert.False(captain.IsEnemy(player));
                Assert.False(captain.IsEnemy(pet));
            }
            finally
            {
                pet.Delete();
                player.Delete();
            }
        });
    }

    [Fact]
    public void MonsterAPlayerIsFighting_IsLeftToThePlayer()
    {
        Run((_, captain) =>
        {
            var player = CreatePlayer();
            var zombie = Place(new Zombie());

            try
            {
                Assert.True(captain.IsEnemy(zombie));

                // Setting a combatant trips over the test host's bare player, so record the fight directly.
                zombie.Aggressed.Add(AggressorInfo.Create(zombie, player, false));

                Assert.True(BandRules.IsClaimedByPlayer(zombie));
                Assert.False(captain.IsEnemy(zombie));
            }
            finally
            {
                zombie.Delete();
                player.Delete();
            }
        });
    }

    [Fact]
    public void BlowsBetweenBandsAndMonsters_AreSoftened()
    {
        Run((band, captain) =>
        {
            var zombie = Place(new Zombie());
            var warrior = band.Members[CompanionRole.Warrior];

            try
            {
                var taken = 20;
                warrior.AlterMeleeDamageFrom(zombie, ref taken);
                Assert.Equal((int)Math.Round(20 * BandSystem.DamageTakenScale), taken);

                var dealt = 20;
                captain.AlterMeleeDamageTo(zombie, ref dealt);
                Assert.Equal((int)Math.Round(20 * BandSystem.DamageDealtScale), dealt);

                var spell = 20;
                warrior.AlterSpellDamageFrom(captain, ref spell);
                Assert.Equal(20, spell);
            }
            finally
            {
                zombie.Delete();
            }
        });
    }

    [Fact]
    public void BandMembers_DrawNoWages_AndNeverRunDry()
    {
        Run((band, _) =>
        {
            var warrior = band.Members[CompanionRole.Warrior];
            var mage = band.Members[CompanionRole.Mage];
            var purse = warrior.Purse;

            for (var i = 0; i <= CompanionSettings.WageDayMinutes * 2; i++)
            {
                warrior.OnMinuteElapsed();
            }

            Assert.Equal(CompanionState.Active, warrior.State);
            Assert.Equal(purse, warrior.Purse);

            warrior.Backpack.ConsumeTotal(typeof(Bandage), warrior.Backpack.GetAmount(typeof(Bandage)));
            warrior.Backpack.ConsumeTotal(typeof(HealPotion), warrior.Backpack.GetAmount(typeof(HealPotion)));
            mage.Backpack.ConsumeTotal(typeof(BlackPearl), mage.Backpack.GetAmount(typeof(BlackPearl)));

            warrior.RestockForBand();
            mage.RestockForBand();

            Assert.Equal(50, warrior.Backpack.GetAmount(typeof(Bandage)));
            Assert.Equal(2, warrior.Backpack.GetAmount(typeof(HealPotion)));
            Assert.Equal(30, mage.Backpack.GetAmount(typeof(BlackPearl)));
        });
    }

    [Fact]
    public void AreaSpells_NeverCatchBandmates()
    {
        Run((band, captain) =>
        {
            var zombie = Place(new Zombie());
            var mage = band.Members[CompanionRole.Mage];

            try
            {
                Assert.False(SpellHelper.ValidIndirectTarget(mage, captain));
                Assert.False(SpellHelper.ValidIndirectTarget(mage, band.Members[CompanionRole.Warrior]));
                Assert.False(SpellHelper.ValidIndirectTarget(captain, mage));
                Assert.True(SpellHelper.ValidIndirectTarget(mage, zombie));
            }
            finally
            {
                zombie.Delete();
            }
        });
    }

    [Fact]
    public void AStrayBlowFromABandmate_IsNeverAnswered()
    {
        Run((band, captain) =>
        {
            var mage = band.Members[CompanionRole.Mage];
            var warrior = band.Members[CompanionRole.Warrior];

            captain.AggressiveAction(mage, false);
            warrior.AggressiveAction(mage, false);

            Assert.Null(captain.Combatant);
            Assert.Equal(OrderType.Follow, warrior.ControlOrder);
            Assert.Same(captain, warrior.ControlTarget);
        });
    }

    [Fact]
    public void AnOrderToAttackABandmate_IsForgotten()
    {
        Run((band, captain) =>
        {
            var warrior = band.Members[CompanionRole.Warrior];

            warrior.ControlTarget = band.Members[CompanionRole.Bard];
            warrior.ControlOrder = OrderType.Attack;
            warrior.ForgiveBandmates();

            Assert.Equal(OrderType.Follow, warrior.ControlOrder);
            Assert.Same(captain, warrior.ControlTarget);
        });
    }

    [Fact]
    public void CaptainFalls_TheBandBreaksUp_AndANewOneIsDue()
    {
        Run((band, captain) =>
        {
            band.OnCaptainFell(captain);

            Assert.Null(band.Captain);
            Assert.True(band.NextMuster > Core.Now);
        });
    }
}
