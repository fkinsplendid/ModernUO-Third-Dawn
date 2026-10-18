using System;
using Server;
using Server.Engines.Companions;
using Server.Engines.TownLife;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class TownLifeTests
{
    // The test host has no NPC speed table; creatures need at least the default class.
    public TownLifeTests() =>
        NPCSpeeds.RegisterSpeed(
            new NPCSpeeds.SpeedClassEntry { Level = SpeedLevel.Medium, ActiveSpeed = 0.2, PassiveSpeed = 0.4, Types = [] }
        );

    // ---------- The day ----------

    [Theory]
    [InlineData(4, DayPhase.Night)]
    [InlineData(5, DayPhase.Dawn)]
    [InlineData(8, DayPhase.Morning)]
    [InlineData(12, DayPhase.Midday)]
    [InlineData(14, DayPhase.Afternoon)]
    [InlineData(18, DayPhase.Evening)]
    [InlineData(22, DayPhase.Night)]
    public void Schedule_HoursMapToTheRightPartOfTheDay(int hour, DayPhase expected) =>
        Assert.Equal(expected, ResidentSchedule.PhaseAt(hour));

    [Fact]
    public void Schedule_EveryTradeHasSomewhereToBeAllDay()
    {
        for (var trade = (ResidentTrade)0; trade < ResidentTrade.Count; trade++)
        {
            for (var phase = (DayPhase)0; phase < DayPhase.Count; phase++)
            {
                Assert.False(ResidentSchedule.ChoicesFor(trade, phase).IsEmpty, $"{trade} at {phase}");
            }
        }
    }

    [Fact]
    public void Schedule_StreetsThinOutAtNight()
    {
        var outAtNight = 0;

        for (var trade = (ResidentTrade)0; trade < ResidentTrade.Count; trade++)
        {
            foreach (var choice in ResidentSchedule.ChoicesFor(trade, DayPhase.Night))
            {
                if (choice.Kind != PlaceKind.Home)
                {
                    outAtNight++;
                    break;
                }
            }
        }

        // The drunk, the night watch, the beggar, the minstrel and some of the bank lurkers; everyone else is home.
        Assert.Equal(5, outAtNight);
    }

    [Fact]
    public void Schedule_PlacesTheTownLacksFallBackToTheStreet()
    {
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(
                PlaceKind.Street,
                ResidentSchedule.Choose(ResidentTrade.Fisher, DayPhase.Morning, kind => kind == PlaceKind.Street)
            );
        }
    }

    [Fact]
    public void Schedule_OnlyChoosesPlacesTheTownHas()
    {
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(
                PlaceKind.Tavern,
                ResidentSchedule.Choose(ResidentTrade.Farmer, DayPhase.Midday, kind => kind == PlaceKind.Tavern)
            );
        }
    }

    // ---------- Finding the way ----------

    /// <summary>
    /// A 20×10 patch of open ground with a wall down x = 10, open only at y = 9.
    /// </summary>
    private static StreetMap WalledYard(bool gapInWall = true)
    {
        var streets = new StreetMap(Map.Trammel, null, new Rectangle2D(100, 100, 20, 10));

        for (var x = 100; x < 120; x++)
        {
            for (var y = 100; y < 110; y++)
            {
                if (x != 110 || gapInWall && y == 109)
                {
                    streets.MarkWalkable(x, y, 0);
                }
            }
        }

        streets.MarkReady();
        return streets;
    }

    [Fact]
    public void Route_GoesRoundTheWallThroughTheGap()
    {
        var route = WalledYard().FindRoute(new Point3D(105, 100, 0), new Point3D(115, 100, 0));

        Assert.NotEmpty(route);
        Assert.Equal(new Point3D(115, 100, 0), route[^1]);

        // It has to pass through the gap at the bottom of the wall.
        var reachedGap = false;
        var previous = new Point3D(105, 100, 0);

        foreach (var waypoint in route)
        {
            Assert.True(waypoint.X != 110 || waypoint.Y == 109, $"waypoint {waypoint} is in the wall");

            if (previous.X < 110 && waypoint.X > 110 || waypoint.X == 110)
            {
                reachedGap = true;
            }

            previous = waypoint;
        }

        Assert.True(reachedGap);
    }

    [Fact]
    public void Route_WaypointsAreAShortWalkApart()
    {
        var route = WalledYard().FindRoute(new Point3D(100, 100, 0), new Point3D(119, 100, 0));
        var previous = new Point3D(100, 100, 0);

        foreach (var waypoint in route)
        {
            Assert.True(Math.Max(Math.Abs(waypoint.X - previous.X), Math.Abs(waypoint.Y - previous.Y)) <= 10);
            previous = waypoint;
        }
    }

    [Fact]
    public void Route_NoneWhenThereIsNoWay() =>
        Assert.Empty(WalledYard(false).FindRoute(new Point3D(105, 100, 0), new Point3D(115, 100, 0)));

    [Fact]
    public void Route_NotReadyMeansNoRoute()
    {
        var streets = new StreetMap(Map.Trammel, null, new Rectangle2D(100, 100, 5, 5));
        streets.MarkWalkable(100, 100, 0);
        streets.MarkWalkable(101, 100, 0);

        Assert.Empty(streets.FindRoute(new Point3D(100, 100, 0), new Point3D(101, 100, 0)));
    }

    // ---------- Conversations ----------

    [Fact]
    public void Dialogues_UseOnlyKnownPlaceholders_AndGossipOnlyWhenMarked()
    {
        foreach (var dialogue in ResidentDialogues.All)
        {
            Assert.True(dialogue.Lines.Length >= 2, string.Join(" / ", dialogue.Lines));

            // News lines are filled from the news itself; tested separately.
            if (dialogue.News != null)
            {
                continue;
            }

            foreach (var line in dialogue.Lines)
            {
                var filled = Conversation.Fill(line, "Ann", "Bo", "Cy");
                Assert.DoesNotContain("{", filled);
                Assert.True(dialogue.NeedsGossip || !line.Contains("{c}"), line);
            }

            Assert.True(!dialogue.WithKeeper || dialogue.Place != null, dialogue.Lines[0]);
            Assert.True(!dialogue.Passing || dialogue.Lines.Length <= 3, dialogue.Lines[0]);
        }
    }

    [Fact]
    public void Dialogues_ChoiceRespectsWhoAndWhere()
    {
        for (var i = 0; i < 300; i++)
        {
            var phase = (DayPhase)Utility.Random((int)DayPhase.Count);
            var place = (PlaceKind)Utility.Random((int)PlaceKind.Count);
            var a = (ResidentTrade)Utility.Random((int)ResidentTrade.Count);
            var listener = Utility.Random(3) switch
            {
                0 => Listener.Keeper,
                1 => Listener.Hireling((CompanionRole)Utility.Random(3)),
                _ => Listener.Neighbour((ResidentTrade)Utility.Random((int)ResidentTrade.Count))
            };
            var passing = listener.Kind == ListenerKind.Resident && Utility.RandomBool();
            var gossip = Utility.RandomBool();

            var d = ResidentDialogues.Choose(a, listener, place, phase, passing, gossip);

            if (d == null)
            {
                continue;
            }

            Assert.Equal(listener.Kind, d.With);
            Assert.Equal(passing, d.Passing);
            Assert.True(d.Place == null || d.Place == place);
            Assert.True(d.TradeA == null || d.TradeA == a);
            Assert.True(d.TradeB == null || d.TradeB == listener.Trade);
            Assert.True(d.Role == null || d.Role == listener.Role);
            Assert.True(d.Phases == null || Array.IndexOf(d.Phases, phase) >= 0);
            Assert.True(gossip || !d.NeedsGossip);
        }
    }

    [Fact]
    public void Dialogues_TheWatchAndTheDrunkUsuallyHaveWords()
    {
        var specific = 0;

        for (var i = 0; i < 200; i++)
        {
            var d = ResidentDialogues.Choose(
                ResidentTrade.Watchman, Listener.Neighbour(ResidentTrade.Drunk), PlaceKind.Street, DayPhase.Night, false, true
            );

            if (d?.TradeA == ResidentTrade.Watchman)
            {
                specific++;
            }
        }

        Assert.True(specific > 40, $"only {specific} of 200");
    }

    [Fact]
    public void Dialogues_LocalTalkStaysInItsTown()
    {
        var local = 0;

        for (var i = 0; i < 400; i++)
        {
            var d = ResidentDialogues.Choose(
                ResidentTrade.Townsfolk, Listener.Neighbour(ResidentTrade.Townsfolk), PlaceKind.Street, DayPhase.Midday,
                false, false, town: "Ocllo"
            );

            Assert.True(d.Town is null or "Ocllo", $"{d.Town} talk in Ocllo");

            if (d.Town == "Ocllo")
            {
                local++;
            }
        }

        Assert.True(local > 0, "no Ocllo talk in Ocllo");
        Assert.Equal("Too busy in Vesper.", Conversation.Fill("Too busy in {town}.", "A", "B", null, town: "Vesper"));
    }

    [Fact]
    public void Dialogues_EveryKeeperPlaceHasSomethingToSay()
    {
        foreach (var place in new[] { PlaceKind.Tavern, PlaceKind.Market, PlaceKind.Bank, PlaceKind.Smithy, PlaceKind.Healer, PlaceKind.Docks })
        {
            Assert.NotNull(ResidentDialogues.Choose(ResidentTrade.Townsfolk, Listener.Keeper, place, DayPhase.Midday, false, false));
        }
    }

    [Fact]
    public void Dialogues_EveryCompanionRoleCanBeTalkedTo()
    {
        for (var role = CompanionRole.Warrior; role <= CompanionRole.Mage; role++)
        {
            Assert.NotNull(
                ResidentDialogues.Choose(ResidentTrade.Townsfolk, Listener.Hireling(role), PlaceKind.Street, DayPhase.Midday, false, false)
            );
        }
    }

    [Fact]
    public void Dialogues_TheMinstrelSizesUpAVisitingBard()
    {
        var rivalry = 0;

        for (var i = 0; i < 200; i++)
        {
            var d = ResidentDialogues.Choose(
                ResidentTrade.Minstrel, Listener.Hireling(CompanionRole.Bard), PlaceKind.Market, DayPhase.Midday, false, false
            );

            if (d?.TradeA == ResidentTrade.Minstrel)
            {
                rivalry++;
            }
        }

        Assert.True(rivalry > 40, $"only {rivalry} of 200");
    }

    // ---------- News ----------

    private static PlayerMobile NewPlayer(string name)
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Name = name;
        World.AddEntity(player);
        return player;
    }

    [Fact]
    public void News_BecomingAGrandmasterIsNews()
    {
        TownNews.Clear();
        var player = NewPlayer("Ana");

        try
        {
            // The player's own skill-change handler reports it.
            player.Skills.Magery.Base = 99.9;
            player.Skills.Magery.Base = 100.0;

            var news = TownNews.About(player, NewsKind.Grandmaster);
            Assert.NotNull(news);
            Assert.Equal("magery", news.What);

            // Already a grandmaster: not news twice.
            TownNews.OnSkillChange(player, SkillName.Magery, 100.0);
            Assert.Single(TownNews.Items);
        }
        finally
        {
            player.Delete();
            TownNews.Clear();
        }
    }

    [Fact]
    public void News_ATownsLossIsOnlyThatTownsTalk()
    {
        TownNews.Clear();

        try
        {
            TownNews.Add(new NewsItem { Kind = NewsKind.ResidentKilled, WhoName = "Ana", What = "Tom", Town = "Minoc", When = Core.Now });

            for (var i = 0; i < 20; i++)
            {
                Assert.Null(TownNews.PickFor("Britain"));
                Assert.NotNull(TownNews.PickFor("Minoc"));
            }
        }
        finally
        {
            TownNews.Clear();
        }
    }

    [Fact]
    public void News_OldNewsIsForgotten()
    {
        TownNews.Clear();
        var now = Core._now;
        Core._now = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

        try
        {
            TownNews.Add(new NewsItem { Kind = NewsKind.Newcomer, WhoName = "Ana", When = Core.Now });
            Assert.NotNull(TownNews.PickFor("Britain"));

            Core._now += TownNews.Lifetime + TimeSpan.FromMinutes(1);
            Assert.Null(TownNews.PickFor("Britain"));
        }
        finally
        {
            Core._now = now;
            TownNews.Clear();
        }
    }

    [Fact]
    public void News_TheTownRemembersWhoKilledOneOfTheirOwn()
    {
        TownNews.Clear();
        var killer = NewPlayer("Ana");
        var witness = new Resident(ResidentTrade.Townsfolk, null);

        try
        {
            TownNews.Add(
                new NewsItem { Kind = NewsKind.ResidentKilled, Who = killer, WhoName = "Ana", What = "Old Tom", When = Core.Now }
            );

            // A resident with no town hears about everyone's losses; one of Minoc's wouldn't.
            var line = ResidentSpeech.Recognise(witness, killer);
            Assert.NotNull(line);
            Assert.Contains("Old Tom", line);
        }
        finally
        {
            killer.Delete();
            witness.Delete();
            TownNews.Clear();
        }
    }

    [Fact]
    public void News_SurvivesARestart()
    {
        TownNews.Clear();
        var player = NewPlayer("Ana");
        var persistence = new TownNews();

        try
        {
            var when = Core.Now;
            TownNews.Add(
                new NewsItem
                {
                    Kind = NewsKind.BigKill, Who = player, WhoName = "Ana", What = "an ogre lord", Where = "Despise",
                    Town = null, When = when
                }
            );

            var writer = new BufferWriter(true);
            persistence.Serialize(writer);
            TownNews.Clear();
            persistence.Deserialize(new BufferReader(writer.Buffer));

            var news = Assert.Single(TownNews.Items);
            Assert.Equal(NewsKind.BigKill, news.Kind);
            Assert.Equal(player, news.Who);
            Assert.Equal("an ogre lord", news.What);
            Assert.Equal("Despise", news.Where);
            Assert.Null(news.Town);
            Assert.Equal(when, news.When);
        }
        finally
        {
            persistence.Unregister();
            player.Delete();
            TownNews.Clear();
        }
    }

    [Fact]
    public void News_EveryNewsLineFillsIn()
    {
        var news = new NewsItem { Kind = NewsKind.BigKill, WhoName = "Ana", What = "an ogre lord", Where = "Despise", When = Core.Now };

        foreach (var dialogue in ResidentDialogues.All)
        {
            if (dialogue.News == null)
            {
                continue;
            }

            foreach (var line in dialogue.Lines)
            {
                Assert.DoesNotContain("{", Conversation.Fill(line, "Bo", "Cy", null, news));
            }
        }

        for (var kind = (NewsKind)0; kind < NewsKind.Count; kind++)
        {
            Assert.NotNull(
                ResidentDialogues.Choose(ResidentTrade.Townsfolk, Listener.Neighbour(ResidentTrade.Farmer), PlaceKind.Market, DayPhase.Midday, false, true, kind)
            );
        }
    }

    // ---------- Reactions ----------

    [Fact]
    public void Reactions_EveryKindHasLinesInEveryManner()
    {
        var resident = new Resident(ResidentTrade.Townsfolk, null);

        try
        {
            for (var kind = (ReactionKind)0; kind < ReactionKind.Count; kind++)
            {
                for (var i = 0; i < 20; i++)
                {
                    var line = ResidentSpeech.Reaction(resident, kind);
                    Assert.False(string.IsNullOrEmpty(line));
                    Assert.DoesNotContain("{", line);
                }
            }
        }
        finally
        {
            resident.Delete();
        }
    }

    [Fact]
    public void Reactions_KnowAMonsterFromAPet()
    {
        var ogre = new Ogre();
        var rat = new SewerRat();
        var horse = new Horse();
        var dragon = new Dragon();
        var cat = new Cat();
        var neighbour = new Resident(ResidentTrade.Farmer, null);

        try
        {
            Assert.True(ResidentAI.IsMonster(ogre));
            Assert.False(ResidentAI.IsMonster(rat));
            Assert.False(ResidentAI.IsMonster(horse));
            Assert.False(ResidentAI.IsMonster(neighbour));

            Assert.True(ResidentAI.IsScaryPet(dragon));
            Assert.False(ResidentAI.IsScaryPet(cat));
            Assert.False(ResidentAI.IsScaryPet(horse));
        }
        finally
        {
            ogre.Delete();
            rat.Delete();
            horse.Delete();
            dragon.Delete();
            cat.Delete();
            neighbour.Delete();
        }
    }

    [Fact]
    public void Reactions_SomeoneWithNothingOnIsUnclothed()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();

        try
        {
            Assert.True(ResidentAI.IsUnclothed(player));
        }
        finally
        {
            player.Delete();
        }
    }

    // ---------- Speech ----------

    [Fact]
    public void Speech_NeverLeavesAPlaceholderUnfilled()
    {
        var resident = new Resident(ResidentTrade.Townsfolk, null);

        try
        {
            for (var i = 0; i < 300; i++)
            {
                var line = ResidentSpeech.Idle(resident);
                Assert.False(line?.Contains('{') == true, line);
            }
        }
        finally
        {
            resident.Delete();
        }
    }

    [Fact]
    public void Speech_TownsfolkFearMurderers()
    {
        var resident = new Resident(ResidentTrade.Noble, null);
        var murderer = new PlayerMobile(World.NewMobile);
        murderer.DefaultMobileInit();
        murderer.Kills = 5;

        try
        {
            var line = ResidentSpeech.Greeting(resident, murderer);
            Assert.Contains(line, new[] { "Keep away from me!", "Murderer...", "*backs away*", "Guards'll have you yet." });
        }
        finally
        {
            resident.Delete();
            murderer.Delete();
        }
    }

    [Fact]
    public void Residents_DressForTheirTrade()
    {
        var fisher = new Resident(ResidentTrade.Fisher, null);
        var smith = new Resident(ResidentTrade.Smith, null);

        try
        {
            Assert.Equal("the fisher", fisher.Title);
            Assert.NotNull(fisher.FindItemOnLayer(Layer.OneHanded) ?? fisher.FindItemOnLayer(Layer.TwoHanded) ??
                           FindInItems<Server.Items.FishingPole>(fisher));
            Assert.NotNull(FindInItems<Server.Items.SmithHammer>(smith));
        }
        finally
        {
            fisher.Delete();
            smith.Delete();
        }
    }

    private static T FindInItems<T>(Mobile m) where T : Item
    {
        foreach (var item in m.Items)
        {
            if (item is T found)
            {
                return found;
            }
        }

        return null;
    }
}
