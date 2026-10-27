using System;
using Server.Mobiles;

namespace Server.Engines.Companions;

/// <summary>
/// Reading the fight. Every perception tick the threats are sorted into brutes (big melee beasts
/// like ogre lords, best handled by setting them on each other or putting blades in their path) and
/// spellcasters (liches and the like, who dispel summons and are best rushed), and the party
/// weighs whether it is outmatched. The roles choose their tactics from this, and the first
/// to notice the party is outmatched says so. Only the employer can call the retreat.
/// Each companion also remembers its last real decision, so it can explain itself ("why").
/// </summary>
public abstract partial class CompanionAI
{
    // A melee beast this big outlasts a whole party trading blows with it.
    private const int BruteHits = 250;

    // Enemy hit points this many times the party's own, the party is outmatched.
    private const double OutmatchedStrength = 1.5;

    private const int OutmatchedCallCooldownMs = 60000;

    private string _decision;
    private long _decisionAt;

    /// <summary>Big melee beasts among the threats.</summary>
    protected int BruteCount { get; private set; }

    /// <summary>Spellcasters among the threats. They dispel summons.</summary>
    protected int CasterCount { get; private set; }

    /// <summary>The threat with the most hit points left.</summary>
    protected Mobile StrongestThreat { get; private set; }

    /// <summary>The enemy has well more fight left in it than the party.</summary>
    protected bool Outmatched => _threats.Count > 0 && EnemyStrength >= OutmatchedStrength;

    public static bool IsSpellcaster(Mobile m) => m.Skills.Magery.Value >= 40.0;

    public static bool IsBrute(Mobile m) => m.HitsMax >= BruteHits && !IsSpellcaster(m);

    private void ReadFight()
    {
        BruteCount = 0;
        CasterCount = 0;
        StrongestThreat = null;

        for (var i = 0; i < _threats.Count; i++)
        {
            var threat = _threats[i];

            if (IsSpellcaster(threat))
            {
                CasterCount++;
            }
            else if (IsBrute(threat))
            {
                BruteCount++;
            }

            if (StrongestThreat == null || threat.Hits > StrongestThreat.Hits)
            {
                StrongestThreat = threat;
            }
        }

        var party = Party;
        var now = Core.TickCount;

        if (Outmatched)
        {
            party.LastOutmatched = now;
        }

        if (Outmatched && !party.IsRetreating && now - party.NextOutmatchedCall >= 0 &&
            Companion.Speak(SpeechContext.Outmatched, CompanionSpeech.BareName(StrongestThreat.Name), urgent: true))
        {
            party.NextOutmatchedCall = now + OutmatchedCallCooldownMs;
        }
    }

    /// <summary>Remembers what this companion just decided to do, for "why".</summary>
    protected void Note(string decision)
    {
        _decision = decision;
        _decisionAt = Core.TickCount;
    }

    /// <summary>How this companion reads the fight, and the last thing it decided.</summary>
    public string Explain()
    {
        string read;

        if (_threats.Count == 0)
        {
            read = Party.IsRetreating ? "We're falling back." : "Nothing's threatening us.";
        }
        else
        {
            var kinds = BruteCount > 0 && CasterCount > 0 ? $", {BruteCount} brute(s) and {CasterCount} caster(s)"
                : BruteCount > 0 ? $", {BruteCount} brute(s)"
                : CasterCount > 0 ? $", {CasterCount} caster(s)"
                : "";

            var odds = Outmatched ? "We're outmatched" : EnemyStrength >= 1.0 ? "It's close" : "We have the upper hand";

            read = $"{_threats.Count} foe(s){kinds}. {odds}.";
        }

        if (_decision == null)
        {
            return read;
        }

        var seconds = Math.Max(0, (Core.TickCount - _decisionAt) / 1000);
        return $"{read} Last thing I did ({seconds}s ago): {_decision}.";
    }
}
