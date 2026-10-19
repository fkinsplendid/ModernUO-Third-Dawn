namespace Server.Engines.TownLife;

/// <summary>
/// What a town crier makes of the news: the same real shard events townsfolk gossip about, cried
/// out properly. Each proclamation is a few short lines, shouted one after another.
/// </summary>
public static class TownCrierNews
{
    /// <summary>Something for the crier of <paramref name="town"/> to proclaim, or null if nothing has happened.</summary>
    public static NewsItem Pick(string town, NewsItem last)
    {
        var item = TownNews.PickFor(town);

        // One more try, so a crier doesn't cry the same thing twice running.
        return item != null && item == last ? TownNews.PickFor(town) : item;
    }

    public static string[] Proclaim(NewsItem item, string town)
    {
        var who = item.WhoName ?? "someone";
        var what = item.What ?? "something";
        var where = item.Where ?? "the wilds";
        var when = When(item);

        return item.Kind switch
        {
            NewsKind.BigKill => Utility.RandomBool()
                ? [$"{who} hath slain {what} in {where}, {when}!", "Let the bards sing of it!"]
                : [$"Word from {where}: {what} lies dead, {when}.", $"It fell to {who}. Britannia thanks thee!"],
            NewsKind.Died => Utility.RandomBool()
                ? [$"{who} fell to {what} in {where}, {when}.", "May the virtues guide them home."]
                : [$"Sad tidings from {where}!", $"{who} was slain by {what}, {when}.", "Travel in company, good people."],
            NewsKind.Murder =>
            [
                $"Foul murder in {where}!",
                $"{who} was slain by {what}, {when}.",
                $"Mark the name {what}, and keep thy distance."
            ],
            NewsKind.Grandmaster => Utility.RandomBool()
                ? ["Hear of a great achievement!", $"{who} is now a Grandmaster of {what}!"]
                : [$"{who} hath mastered {what}, {when}.", "None in the land is more skilled!"],
            NewsKind.Newcomer => Utility.RandomBool()
                ? [$"A new adventurer hath come to Britannia: {who}!", "Make them welcome."]
                : [$"Welcome, {who}, to these lands!", "Mind the dungeons, and the drunks."],
            NewsKind.ResidentKilled =>
            [
                $"Grief in {item.Town ?? town}!",
                $"{what}, one of our own, was slain {when}.",
                $"The killer is {who}. Shun them, good people!"
            ],
            NewsKind.CompanionLost =>
            [
                $"{what}, sworn companion of {who}, fell in {where}, {when}.",
                "Loyal to the last."
            ],
            _ => null
        };
    }

    private static string When(NewsItem item) =>
        (Core.Now - item.When).TotalHours switch
        {
            < 1  => "this very hour",
            < 12 => "this day",
            < 36 => "but yesterday",
            _    => "of late"
        };
}
