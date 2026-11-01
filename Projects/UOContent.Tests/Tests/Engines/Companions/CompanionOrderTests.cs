using System;
using Server;
using Server.Engines.Companions;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

/// <summary>
/// A companion must never be left without something to do. The engine rewrites a pet's order when
/// anything attacks it and never reconsiders that order on its own, so these follow an order from
/// the moment it is taken over to the moment the fight is over.
/// </summary>
[Collection("Sequential UOContent Tests")]
public class CompanionOrderTests : IDisposable
{
    private readonly Expansion _expansion;

    // Felucca, well away from the coordinates other test classes use.
    private static readonly Point3D Origin = new(5240, 3840, 0);

    public CompanionOrderTests()
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
        player.MoveToWorld(Origin, Map.Felucca);
        return player;
    }

    private static Mongbat CreateMonster(int tilesAway)
    {
        var monster = new Mongbat();
        monster.MoveToWorld(new Point3D(Origin.X + tilesAway, Origin.Y, Origin.Z), Map.Felucca);
        return monster;
    }

    private static void AssertFollowing(BaseCompanion companion, Mobile employer)
    {
        Assert.Equal(OrderType.Follow, companion.ControlOrder);
        Assert.Same(employer, companion.ControlTarget);
    }

    [Fact]
    public void AttackedWhileFollowing_FightsWhileTheAttackerIsInReach()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var monster = CreateMonster(1);

        try
        {
            companion.BeginService(employer);
            monster.Combatant = companion;

            Assert.Equal(OrderType.Attack, companion.ControlOrder);

            companion.AIObject.Obey();

            Assert.Equal(OrderType.Attack, companion.ControlOrder);
            Assert.Same(monster, companion.ControlTarget);
            Assert.Same(monster, companion.Combatant);
        }
        finally
        {
            monster.Delete();
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void AttackerGetsAway_GoesBackToFollowing()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var monster = CreateMonster(1);

        try
        {
            companion.BeginService(employer);
            monster.Combatant = companion;
            companion.AIObject.Obey();

            monster.Combatant = null;
            monster.MoveToWorld(new Point3D(Origin.X + 30, Origin.Y, Origin.Z), Map.Felucca);
            companion.AIObject.Obey();

            AssertFollowing(companion, employer);
        }
        finally
        {
            monster.Delete();
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void AttackerHides_GoesBackToFollowing()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var monster = CreateMonster(1);

        try
        {
            companion.BeginService(employer);
            monster.Combatant = companion;
            companion.AIObject.Obey();

            monster.Hidden = true;
            companion.AIObject.Obey();

            AssertFollowing(companion, employer);
        }
        finally
        {
            monster.Delete();
            companion.Delete();
            employer.Delete();
        }
    }

    /// <summary>A healer spends its ticks healing, so it may never get round to the fight it was pulled into.</summary>
    [Fact]
    public void AttackerDiesBeforeTheCompanionEverFought_GoesBackToFollowing()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var monster = CreateMonster(1);

        try
        {
            companion.BeginService(employer);
            monster.Combatant = companion;
            monster.Delete();

            for (var i = 0; i < 3; i++)
            {
                Assert.True(companion.AIObject.Obey());
            }

            AssertFollowing(companion, employer);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void CallingARetreat_DropsTheAttackOrder()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var monster = CreateMonster(1);

        try
        {
            companion.BeginService(employer);
            monster.Combatant = companion;
            companion.AIObject.Obey();

            ((CompanionAI)companion.AIObject).OrderRetreat();
            companion.AIObject.Obey();

            AssertFollowing(companion, employer);
        }
        finally
        {
            monster.Delete();
            companion.Delete();
            employer.Delete();
        }
    }

    [Theory]
    [InlineData(OrderType.None)]
    [InlineData(OrderType.Rename)]
    public void LeftWithNoRealOrder_GoesBackToFollowing(OrderType order)
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            companion.ControlTarget = null;
            companion.ControlOrder = order;

            Assert.True(companion.AIObject.Obey());
            AssertFollowing(companion, employer);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Theory]
    [InlineData(OrderType.Stay)]
    [InlineData(OrderType.Stop)]
    public void StandingOrders_AreKept(OrderType order)
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            companion.ControlTarget = null;
            companion.ControlOrder = order;

            Assert.True(companion.AIObject.Obey());
            Assert.Equal(order, companion.ControlOrder);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void ThreatPastPerception_DoesNotStopTheirWalking()
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();
        var monster = CreateMonster(14);

        try
        {
            companion.BeginService(employer);
            employer.Aggressors.Add(AggressorInfo.Create(monster, employer, false));
            companion.AIObject.Obey();

            // Seen as a threat, but past the range combat works at: following, not rooted in a fight.
            Assert.NotEqual(ActionType.Combat, companion.AIObject.Action);
            Assert.Null(companion.FocusMob);
        }
        finally
        {
            monster.Delete();
            companion.Delete();
            employer.Delete();
        }
    }
}
