using System;
using Server.Engines.VeteranRewards;
using Server.Items;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class RewardSystemTests
{
    public RewardSystemTests() => RewardSystem.Configure();

    [Theory]
    [InlineData(30, 500, 0)]   // a month old: nothing, however much played
    [InlineData(400, 50, 0)]   // a year old but barely played
    [InlineData(400, 120, 1)]  // a year and 100+ hours
    [InlineData(800, 150, 1)]  // two years, but only one reward's worth of play
    [InlineData(800, 200, 2)]
    public void RewardLevel_NeedsAYearAndPlayTimeForEach(double ageDays, double hours, int expected)
    {
        Assert.Equal(
            expected,
            RewardSystem.GetRewardLevel(TimeSpan.FromDays(ageDays), TimeSpan.FromHours(hours))
        );
    }

    [Fact]
    public void OnlyStatuettesAndDyeTubs_AreOffered()
    {
        foreach (var list in RewardSystem.Lists)
        {
            foreach (var entry in list.Entries)
            {
                Assert.True(
                    entry.ItemType == typeof(MonsterStatuette) || entry.ItemType.IsSubclassOf(typeof(DyeTub)),
                    entry.ItemType.Name
                );
            }
        }
    }
}
