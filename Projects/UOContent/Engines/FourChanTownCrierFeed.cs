using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Logging;
using Server.Mobiles;

namespace Server.Engines;

public static class FourChanTownCrierFeed
{
    private const string FeedPath = "Data/town-crier-feed.json";
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(FourChanTownCrierFeed));
    private static string _lastFetchedAt;
    private static TimerExecutionToken _timerToken;

    public static void Configure()
    {
        Timer.StartTimer(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            CheckFeed,
            out _timerToken
        );
    }

    private static void CheckFeed()
    {
        var path = Path.Combine(Core.BaseDirectory, FeedPath);

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var feed = JsonSerializer.Deserialize<FeedData>(json);

            if (feed?.Lines == null || feed.Lines.Length == 0 || feed.FetchedAt == _lastFetchedAt)
            {
                return;
            }

            _lastFetchedAt = feed.FetchedAt;
            GlobalTownCrierEntryList.Instance.AddEntry(feed.Lines, TimeSpan.FromHours(6));
            _logger.Information("Town crier feed updated ({LineCount} lines).", feed.Lines.Length);
        }
        catch (Exception e)
        {
            _logger.Warning(e, "Failed to read town crier feed from {Path}.", path);
        }
    }

    private record FeedData
    {
        [JsonPropertyName("lines")]
        public string[] Lines { get; init; }

        [JsonPropertyName("fetched_at")]
        public string FetchedAt { get; init; }
    }
}
