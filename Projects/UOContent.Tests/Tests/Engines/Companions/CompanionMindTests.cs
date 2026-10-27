using System;
using Server;
using Server.Engines.Companions;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class CompanionMindTests : IDisposable
{
    private readonly Expansion _expansion;

    // Felucca, away from the other companion tests. No regions are loaded in the test host, so
    // this counts as open country: not a town, not a dungeon.
    private static readonly Point3D Origin = new(5240, 3840, 0);

    public CompanionMindTests()
    {
        _expansion = Core.Expansion;
        Core.Expansion = Expansion.UOTD;

        CompanionSettings.Configure();
        Server.Spells.Initializer.Configure();

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

    private static void ServeOneDay(BaseCompanion companion)
    {
        for (var i = 0; i < CompanionSettings.WageDayMinutes; i++)
        {
            companion.OnMinuteElapsed();
        }
    }

    private static void Run(Action<PlayerMobile, BaseCompanion> test)
    {
        var employer = CreateEmployer();
        var companion = new CompanionWarrior();

        try
        {
            Assert.True(companion.BeginService(employer));
            test(employer, companion);
        }
        finally
        {
            companion.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void NewHire_HasACharacterOfTheirOwn()
    {
        var companion = new CompanionBard();

        try
        {
            Assert.NotEqual(CompanionFear.None, companion.Fear);
            Assert.NotEqual(CompanionFondness.None, companion.Fondness);
            Assert.False(string.IsNullOrEmpty(companion.Hometown));
            Assert.Equal(CompanionMood.Content, companion.Mood);
        }
        finally
        {
            companion.Delete();
        }
    }

    [Fact]
    public void Regard_IsClamped_AndDevotionShavesTheWage() =>
        Run((_, companion) =>
        {
            var fullWage = CompanionSettings.DailyWageFor(companion.Level);

            companion.AdjustRegard(500);

            Assert.Equal(BaseCompanion.MaxRegard, companion.Regard);
            Assert.Equal(CompanionMood.Devoted, companion.Mood);
            Assert.Equal(fullWage * 90 / 100, companion.DailyWage);
        });

    [Fact]
    public void LoyalCompanion_WorksOneDayOnCredit_ThenLeavesIfStillUnpaid() =>
        Run((_, companion) =>
        {
            companion.AdjustRegard(60);
            companion.Purse = 0;

            ServeOneDay(companion);

            Assert.Equal(CompanionState.Active, companion.State);
            Assert.True(companion.Owed > 0);

            ServeOneDay(companion);

            Assert.Equal(CompanionState.Resting, companion.State);
        });

    [Fact]
    public void ContentCompanion_LeavesUnpaid_AndRemembersIt() =>
        Run((_, companion) =>
        {
            // A love of the open road could lift their spirits during the day and soften the knock.
            companion.Fondness = CompanionFondness.Glory;

            var before = companion.Regard;
            companion.Purse = 0;

            ServeOneDay(companion);

            Assert.Equal(CompanionState.Resting, companion.State);
            Assert.Equal(0, companion.Owed);
            Assert.True(companion.Regard <= before - 20);
        });

    [Fact]
    public void Paying_SettlesWhatIsOwedFirst() =>
        Run((employer, companion) =>
        {
            companion.AdjustRegard(60);
            companion.Purse = 0;
            ServeOneDay(companion);

            var owed = companion.Owed;
            Assert.True(companion.OnDragDrop(employer, new Gold(owed + 100)));

            Assert.Equal(0, companion.Owed);
            Assert.Equal(100, companion.Purse);
        });

    [Fact]
    public void Bitter_GivesNotice_AndATurnAroundWithdrawsIt() =>
        Run((_, companion) =>
        {
            companion.AdjustRegard(-95);

            Assert.True(companion.GaveNotice);
            Assert.Equal(CompanionIssue.Notice, companion.AskedIssue);

            companion.AdjustRegard(50);

            Assert.False(companion.GaveNotice);
            Assert.Equal(CompanionState.Active, companion.State);
        });

    [Fact]
    public void Bitter_ToldNo_LeavesForTheGuild() =>
        Run((employer, companion) =>
        {
            companion.AdjustRegard(-95);

            Assert.True(companion.AnswerIssue(IssueAnswer.No));

            companion.ThinkMind(Core.TickCount);

            Assert.Equal(CompanionState.Resting, companion.State);
            Assert.Equal(0, employer.Followers);
        });

    [Fact]
    public void Exhausted_AsksToRest_AndYesIsAPromise() =>
        Run((_, companion) =>
        {
            companion.Weariness = BaseCompanion.ExhaustedWeariness;
            var before = companion.Regard;

            companion.ThinkMind(Core.TickCount);

            Assert.Equal(CompanionIssue.Weary, companion.AskedIssue);
            Assert.True(companion.AnswerIssue(IssueAnswer.Yes));
            Assert.Equal(CompanionIssue.None, companion.AskedIssue);
            Assert.Equal(CompanionIssue.Weary, companion.Promise);
            Assert.True(companion.Regard > before);
        });

    [Fact]
    public void QuestionIgnored_CostsALittle() =>
        Run((_, companion) =>
        {
            companion.Weariness = BaseCompanion.ExhaustedWeariness;
            var now = Core.TickCount;

            companion.ThinkMind(now);
            Assert.Equal(CompanionIssue.Weary, companion.AskedIssue);

            var before = companion.Regard;
            companion.ThinkMind(now + 61000);

            Assert.Equal(CompanionIssue.None, companion.AskedIssue);
            Assert.Equal(before - 1, companion.Regard);
        });

    [Fact]
    public void NoQuestion_NoAnswerTaken() =>
        Run((_, companion) => Assert.False(companion.AnswerIssue(IssueAnswer.Yes)));

    [Fact]
    public void Praise_CountsOncePerWhile_AndAnApologyMendsHalfAnInsult() =>
        Run((_, companion) =>
        {
            var start = companion.Regard;

            companion.TalkAbout(CompanionTopic.Praise);
            companion.TalkAbout(CompanionTopic.Praise);
            Assert.Equal(start + 3, companion.Regard);

            companion.TalkAbout(CompanionTopic.Insult);
            Assert.Equal(start - 3, companion.Regard);

            companion.TalkAbout(CompanionTopic.Apology);
            Assert.Equal(start, companion.Regard);

            // Already forgiven.
            companion.TalkAbout(CompanionTopic.Apology);
            Assert.Equal(start, companion.Regard);
        });

    [Fact]
    public void FacingAFearOftenEnough_ConquersIt() =>
        Run((_, companion) =>
        {
            var spider = new GiantSpider();

            try
            {
                companion.Fear = CompanionFear.Spiders;
                Assert.True(companion.Fears(spider));

                var halfway = 0.0;

                for (var i = 0; i < 25; i++)
                {
                    companion.OnPartyKill(spider, 1);

                    if (i == 11)
                    {
                        halfway = companion.FearStrength;
                    }
                }

                Assert.InRange(halfway, 0.4, 0.6);
                Assert.Equal(CompanionFear.None, companion.Fear);
                Assert.Equal(CompanionFear.Spiders, companion.ConqueredFear);
                Assert.False(companion.Fears(spider));
            }
            finally
            {
                spider.Delete();
            }
        });

    [Theory]
    [InlineData("how are you?", CompanionTopic.Wellbeing)]
    [InlineData("how do you feel about me", CompanionTopic.Opinion)]
    [InlineData("what's on your mind", CompanionTopic.Concerns)]
    [InlineData("what scares you?", CompanionTopic.Fear)]
    [InlineData("what do you love most", CompanionTopic.Fondness)]
    [InlineData("tell me about yourself", CompanionTopic.Story)]
    [InlineData("tell me about your home town", CompanionTopic.Story)]
    [InlineData("well done!", CompanionTopic.Praise)]
    [InlineData("sorry about that", CompanionTopic.Apology)]
    [InlineData("you useless oaf", CompanionTopic.Insult)]
    public void Talk_RecognisesTopics(string said, CompanionTopic expected)
    {
        Assert.True(CompanionTalk.TryGetTopic(said, out var topic));
        Assert.Equal(expected, topic);
    }

    [Theory]
    [InlineData("gate to minoc")]
    [InlineData("share")]
    [InlineData("mount up")]
    public void Talk_LeavesOrdersAlone(string said) => Assert.False(CompanionTalk.TryGetTopic(said, out _));

    [Theory]
    [InlineData("yes", IssueAnswer.Yes)]
    [InlineData("Aye, I promise.", IssueAnswer.Yes)]
    [InlineData(", yes", IssueAnswer.Yes)]
    [InlineData("no", IssueAnswer.No)]
    [InlineData("No!", IssueAnswer.No)]
    [InlineData("not now", IssueAnswer.Later)]
    [InlineData("later", IssueAnswer.Later)]
    public void Talk_RecognisesAnswers(string said, IssueAnswer expected)
    {
        Assert.True(CompanionTalk.TryGetAnswer(said, out var answer));
        Assert.Equal(expected, answer);
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("yesterday was fun")]
    [InlineData("status")]
    public void Talk_IgnoresWhatIsNotAnAnswer(string said) => Assert.False(CompanionTalk.TryGetAnswer(said, out _));

    [Fact]
    public void Speech_EveryIssueFearAndFondnessHasLines()
    {
        for (var issue = CompanionIssue.Weary; issue <= CompanionIssue.Notice; issue++)
        {
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.IssueLine(issue)), $"{issue}");

            foreach (var answer in (ReadOnlySpan<IssueAnswer>)[IssueAnswer.Yes, IssueAnswer.No, IssueAnswer.Later])
            {
                Assert.False(string.IsNullOrEmpty(CompanionSpeech.IssueReply(issue, answer)), $"{issue} {answer}");
            }

            Assert.False(string.IsNullOrEmpty(CompanionSpeech.PromiseLine(issue, true)), $"{issue}");
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.PromiseLine(issue, false)), $"{issue}");
        }

        for (var fear = CompanionFear.Spiders; fear <= CompanionFear.TheDark; fear++)
        {
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.FearConfession(fear)), $"{fear}");
        }

        for (var fondness = CompanionFondness.Music; fondness <= CompanionFondness.Home; fondness++)
        {
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.FondnessTalk(fondness)), $"{fondness}");
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.FondnessLine(fondness)), $"{fondness}");
        }

        for (var mood = CompanionMood.Bitter; mood <= CompanionMood.Devoted; mood++)
        {
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.MoodLine(mood)), $"{mood}");
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.OpinionLine(mood)), $"{mood}");
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.NothingOnMind(mood, false)), $"{mood}");
        }

        for (var story = 0; story < 500; story += 37)
        {
            foreach (var role in (ReadOnlySpan<CompanionRole>)[CompanionRole.Warrior, CompanionRole.Bard, CompanionRole.Mage])
            {
                Assert.Contains("{arg}", CompanionSpeech.OriginLine(role, story));
            }

            Assert.False(string.IsNullOrEmpty(CompanionSpeech.MemoryLine(story)));
            Assert.False(string.IsNullOrEmpty(CompanionSpeech.DreamLine(story)));
        }
    }

    [Fact]
    public void Fears_MatchTheirFoes()
    {
        var spider = new GiantSpider();
        var skeleton = new Skeleton();
        var orc = new Orc();

        try
        {
            Assert.Equal(CompanionFear.Spiders, CompanionSpeech.FearOf(spider));
            Assert.Equal(CompanionFear.Undead, CompanionSpeech.FearOf(skeleton));
            Assert.Equal(CompanionFear.None, CompanionSpeech.FearOf(orc));
        }
        finally
        {
            spider.Delete();
            skeleton.Delete();
            orc.Delete();
        }
    }
}
