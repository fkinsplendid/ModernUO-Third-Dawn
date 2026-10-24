using System;

namespace Server.Engines.Companions;

/// <summary>Conversation with the employer: answers to their questions, and replies that must be said.</summary>
public abstract partial class BaseCompanion
{
    private const int StoryChapters = 3;

    /// <summary>A reply to the employer. Said even if they spoke a moment ago, and even when told to be quiet.</summary>
    private void Answer(SpeechContext context, string arg = null) => Answer(CompanionSpeech.Pick(context, Personality, Role), arg);

    private void Answer(string line, string arg = null)
    {
        if (line == null || Deleted)
        {
            return;
        }

        Say(CompanionSpeech.Format(line, this, arg));
        _nextSpeech = Core.TickCount + UrgentSpeechDelayMs;
    }

    /// <summary>Two lines, the second a moment after the first.</summary>
    private void AnswerTwice(string first, string second, string arg = null)
    {
        Answer(first, arg);

        if (second != null)
        {
            Timer.StartTimer(TimeSpan.FromSeconds(2.5), () => Answer(second, arg));
        }
    }

    public void TalkAbout(CompanionTopic topic)
    {
        if (Deleted || State != CompanionState.Active)
        {
            return;
        }

        if (IsDeadPet)
        {
            Answer(SpeechContext.GhostPlea);
            return;
        }

        switch (topic)
        {
            case CompanionTopic.Wellbeing:
                {
                    var tired = IsExhausted ? CompanionSpeech.ExhaustedLine() : IsTired ? CompanionSpeech.TiredLine() : null;
                    var mood = CompanionSpeech.MoodLine(Mood);
                    AnswerTwice(tired ?? mood, tired != null ? mood : null);
                    break;
                }
            case CompanionTopic.Opinion:
                {
                    Answer(CompanionSpeech.OpinionLine(Mood));
                    break;
                }
            case CompanionTopic.Concerns:
                {
                    TellConcerns();
                    break;
                }
            case CompanionTopic.Fear:
                {
                    TellFear();
                    break;
                }
            case CompanionTopic.Fondness:
                {
                    Answer(CompanionSpeech.FondnessTalk(Fondness), Hometown);
                    break;
                }
            case CompanionTopic.Story:
                {
                    TellStory();
                    break;
                }
            case CompanionTopic.Praise:
                {
                    OnPraised();
                    break;
                }
            case CompanionTopic.Apology:
                {
                    OnApology();
                    break;
                }
            case CompanionTopic.Insult:
                {
                    OnInsulted();
                    break;
                }
        }
    }

    private void TellFear()
    {
        if (Fear != CompanionFear.None)
        {
            var progress = FearStrength < 0.5 ? CompanionSpeech.FearEasingLine() : null;

            // A bitter companion won't admit to weakness in front of someone they despise.
            if (Mood == CompanionMood.Bitter)
            {
                Answer(SpeechContext.StoryRefused);
                return;
            }

            AnswerTwice(CompanionSpeech.FearConfession(Fear), progress, FearName);
            return;
        }

        if (ConqueredFear != CompanionFear.None)
        {
            Answer(SpeechContext.FearOvercome, CompanionSpeech.FearName(ConqueredFear));
            return;
        }

        Answer(SpeechContext.Fearless);
    }

    /// <summary>A little more each time they're asked: where they're from, a memory, what they hope for.</summary>
    private void TellStory()
    {
        if (Mood == CompanionMood.Bitter)
        {
            Answer(SpeechContext.StoryRefused);
            return;
        }

        var chapter = _storyTold++ % StoryChapters;

        // Someone unhappy with you only tells you where they're from.
        if (Mood == CompanionMood.Unhappy)
        {
            chapter = 0;
        }

        var line = chapter switch
        {
            0 => CompanionSpeech.OriginLine(Role, Story),
            1 => CompanionSpeech.MemoryLine(Story),
            _ => CompanionSpeech.DreamLine(Story)
        };

        Answer(line, Hometown);
    }
}

public enum CompanionTopic
{
    Wellbeing,
    Opinion,
    Concerns,
    Fear,
    Fondness,
    Story,
    Praise,
    Apology,
    Insult
}

/// <summary>
/// Recognises talk addressed to a companion, as opposed to orders, and the employer's answers
/// to whatever they've asked.
/// </summary>
public static class CompanionTalk
{
    // Checked in order: "how do you feel about me" is an opinion, not wellbeing.
    private static readonly (CompanionTopic Topic, string[] Phrases)[] _topics =
    [
        (CompanionTopic.Opinion, [
            "think of me", "think about me", "feel about me", "do you like me", "am i a good", "how am i doing",
            "do you trust me", "are you happy with me", "happy working for me"
        ]),
        (CompanionTopic.Concerns, [
            "on your mind", "anything wrong", "what's wrong", "whats wrong", "what is wrong", "concern", "troubling you",
            "bothering you", "what's the matter", "whats the matter", "any problems", "complaint", "anything to say"
        ]),
        (CompanionTopic.Fear, ["afraid", "scare", "fear", "frighten"]),
        (CompanionTopic.Fondness, [
            "what do you like", "what do you love", "what do you enjoy", "favourite", "favorite", "makes you happy"
        ]),
        (CompanionTopic.Story, [
            "about yourself", "your story", "where are you from", "where do you hail", "where's home", "wheres home",
            "where is home", "who are you", "your past", "your family", "your life", "why did you become",
            "how did you become", "what do you want", "your dream", "hope for", "your home", "hometown", "home town"
        ]),
        (CompanionTopic.Wellbeing, [
            "how are you", "how do you feel", "how goes", "how are things", "how're you", "you alright", "you all right",
            "are you ok", "you okay", "how fare you", "how do you fare", "holding up", "tired"
        ]),
        (CompanionTopic.Apology, ["sorry", "apolog", "forgive me", "my fault", "i was wrong"]),
        (CompanionTopic.Insult, [
            "useless", "idiot", "fool", "worthless", "incompetent", "pathetic", "shut up", "stupid", "coward",
            "imbecile", "moron", "oaf"
        ]),
        (CompanionTopic.Praise, [
            "well done", "good work", "good job", "nice work", "nice job", "great work", "great job", "thank you",
            "thanks", "good show", "excellent", "you did well", "well fought", "bravo", "good fight", "nicely done"
        ])
    ];

    private static readonly string[] _yes =
    [
        "yes", "aye", "yea", "yeah", "yep", "alright", "all right", "ok", "okay", "agreed", "very well", "fine",
        "of course", "certainly", "sure", "i promise", "we will", "we shall", "i will", "i shall", "deal", "you have my word",
        "promise", "i'm sorry", "sorry"
    ];

    private static readonly string[] _no = ["no", "nay", "nope", "never", "no way", "absolutely not", "certainly not", "denied"];

    private static readonly string[] _later = ["later", "not now", "soon", "in a while", "maybe later", "we'll see", "perhaps", "maybe"];

    /// <summary>The topic of <paramref name="command"/>, if it's talk rather than an order.</summary>
    public static bool TryGetTopic(string command, out CompanionTopic topic)
    {
        foreach (var (t, phrases) in _topics)
        {
            foreach (var phrase in phrases)
            {
                if (command.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    topic = t;
                    return true;
                }
            }
        }

        topic = default;
        return false;
    }

    /// <summary>A bare answer, "yes", "no" or "later", with nothing else said.</summary>
    public static bool TryGetAnswer(string speech, out IssueAnswer answer)
    {
        var text = speech.Trim().TrimStart(',', ':', ' ').TrimEnd('.', '!', ',').Trim();

        if (Matches(text, _later))
        {
            answer = IssueAnswer.Later;
            return true;
        }

        if (Matches(text, _no))
        {
            answer = IssueAnswer.No;
            return true;
        }

        if (Matches(text, _yes))
        {
            answer = IssueAnswer.Yes;
            return true;
        }

        answer = default;
        return false;
    }

    private static bool Matches(string text, string[] words)
    {
        foreach (var word in words)
        {
            // "yes", "yes, we'll rest", "aye, I promise"
            if (text.InsensitiveEquals(word) ||
                text.StartsWith(word, StringComparison.OrdinalIgnoreCase) && text.Length > word.Length &&
                text[word.Length] is ',' or ' ' or '.' or '!')
            {
                return true;
            }
        }

        return false;
    }
}
