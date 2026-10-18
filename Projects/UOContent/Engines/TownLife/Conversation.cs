using System;
using System.Collections.Generic;

namespace Server.Engines.TownLife;

/// <summary>
/// Two people talking: lines said in turn a few seconds apart, each speaker turning to the
/// other. Longer talks hold both residents in place; a word in passing doesn't, nor does talk
/// with someone's companion, who keeps walking with their employer. It ends early if either
/// leaves, dies, or goes indoors.
/// </summary>
public sealed class Conversation
{
    private const int MaxApart = 8;

    // Two people walking past each other separate fast.
    private const int MaxApartPassing = 12;
    private static readonly TimeSpan PassingReplyDelay = TimeSpan.FromSeconds(1.4);
    private static readonly TimeSpan FirstLineDelay = TimeSpan.FromSeconds(0.5);

    private static readonly Dictionary<Mobile, Conversation> _active = [];
    // Vendors and companions, who have no resident AI to keep their own time between chats.
    private static readonly Dictionary<Mobile, long> _rest = [];

    private readonly Mobile _a;
    private readonly Mobile _b;
    private readonly string[] _lines;
    private int _next;
    private TimerExecutionToken _timer;

    private Conversation(Mobile a, Mobile b, string[] lines, bool holds)
    {
        _a = a;
        _b = b;
        _lines = lines;
        Holds = holds;
    }

    /// <summary>Both stand still for it.</summary>
    public bool Holds { get; }

    public int LineCount => _lines.Length;

    public static Conversation Of(Mobile m) => m != null && _active.TryGetValue(m, out var talk) ? talk : null;

    public static bool IsTalking(Mobile m) => m != null && _active.ContainsKey(m);

    /// <summary>The other person in this conversation.</summary>
    public Mobile PartnerOf(Mobile m) => m == _a ? _b : _a;

    /// <summary>A vendor or companion isn't buttonholed by every passing resident.</summary>
    public static bool IsFree(Mobile m, long now) =>
        !IsTalking(m) && (!_rest.TryGetValue(m, out var until) || now - until >= 0);

    /// <summary>
    /// Begins <paramref name="dialogue"/> between <paramref name="a"/> (who opens) and <paramref name="b"/>.
    /// </summary>
    public static Conversation Start(Mobile a, Mobile b, Dialogue dialogue, string gossipName, NewsItem news = null)
    {
        if (IsTalking(a) || IsTalking(b))
        {
            return null;
        }

        var lines = new string[dialogue.Lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = Fill(dialogue.Lines[i], a.Name, b.Name, gossipName, news, (a as Resident)?.Town?.Name);
        }

        var talk = new Conversation(a, b, lines, !dialogue.Passing && dialogue.With != ListenerKind.Companion);
        _active[a] = talk;
        _active[b] = talk;

        if (dialogue.With != ListenerKind.Resident)
        {
            PruneRest();
            _rest[b] = Core.TickCount + (dialogue.With == ListenerKind.Companion ? 180000 : 60000);
        }

        Timer.StartTimer(FirstLineDelay, talk.SayNext, out talk._timer);
        return talk;
    }

    public static string Fill(string line, string a, string b, string c, NewsItem news = null, string town = null)
    {
        line = line.Replace("{a}", a).Replace("{b}", b).Replace("{c}", c ?? "someone").Replace("{town}", town ?? "town");
        return news == null ? line : FillNews(line, news);
    }

    public static string FillNews(string line, NewsItem news) =>
        line.Replace("{who}", news.WhoName ?? "someone")
            .Replace("{what}", news.What ?? "something")
            .Replace("{where}", news.Where ?? "the wilds");

    private void SayNext()
    {
        var speaker = _next % 2 == 0 ? _a : _b;
        var listener = PartnerOf(speaker);

        if (!CanCarryOn(speaker) || !CanCarryOn(listener) || !speaker.InRange(listener, Holds ? MaxApart : MaxApartPassing))
        {
            End();
            return;
        }

        speaker.Direction = speaker.GetDirectionTo(listener);
        speaker.Say(_lines[_next++]);

        if (_next >= _lines.Length)
        {
            // Let the last line hang a moment before either moves on.
            Timer.StartTimer(TimeSpan.FromSeconds(2), End, out _timer);
            return;
        }

        // Longer lines take longer to say; a word in passing gets a quick answer.
        var delay = Holds
            ? TimeSpan.FromSeconds(Math.Clamp(1.8 + _lines[_next - 1].Length * 0.045, 2.5, 4.5))
            : PassingReplyDelay;
        Timer.StartTimer(delay, SayNext, out _timer);
    }

    private static bool CanCarryOn(Mobile m) =>
        m is { Deleted: false, Alive: true } && m.Map != null && m.Map != Map.Internal &&
        (m is not Resident { AIObject: ResidentAI { IsFleeing: true } });

    public void End()
    {
        _timer.Cancel();

        if (_active.TryGetValue(_a, out var a) && a == this)
        {
            _active.Remove(_a);
        }

        if (_active.TryGetValue(_b, out var b) && b == this)
        {
            _active.Remove(_b);
        }
    }

    private static void PruneRest()
    {
        if (_rest.Count < 64)
        {
            return;
        }

        var now = Core.TickCount;
        var stale = new List<Mobile>();

        foreach (var (m, until) in _rest)
        {
            if (m.Deleted || now - until >= 0)
            {
                stale.Add(m);
            }
        }

        foreach (var m in stale)
        {
            _rest.Remove(m);
        }
    }
}
