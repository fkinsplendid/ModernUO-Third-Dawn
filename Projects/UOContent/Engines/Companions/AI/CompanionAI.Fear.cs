namespace Server.Engines.Companions;

/// <summary>
/// Fear in a fight. The first time a companion is about to take on what they dread, they balk:
/// for a few seconds they go for anything else, or hang back with the employer if there's nothing
/// else. The fresher the fear, the longer they balk. Then they steel themselves and fight on for
/// the rest of that fight. A feared foe going for a badly hurt employer cuts the hesitation short.
/// </summary>
public abstract partial class CompanionAI
{
    private const int MinHesitationMs = 1500;
    private const int MaxExtraHesitationMs = 3500;

    private long _hesitateUntil;
    private bool _steeled;

    /// <summary>Fought within the last minute.</summary>
    public bool FoughtRecently => FoughtWithin(60000);

    /// <summary>
    /// The target to take on given what the companion fears: <paramref name="target"/> itself, another
    /// threat while they balk, or null to hang back.
    /// </summary>
    private Mobile FaceFear(Mobile target)
    {
        if (_steeled || !Companion.Fears(target))
        {
            return target;
        }

        var now = Core.TickCount;
        var name = CompanionSpeech.BareName(target.Name);

        if (_hesitateUntil == 0)
        {
            var employer = Employer;

            if (target.Combatant == employer && employer is { Alive: true } && employer.Hits < employer.HitsMax * 0.4)
            {
                _steeled = true;
                Companion.Speak(SpeechContext.FearBraved, name, urgent: true);
                Note($"braved {target.Name} to save {employer.Name}");
                return target;
            }

            _hesitateUntil = now + MinHesitationMs + (long)(MaxExtraHesitationMs * Companion.FearStrength);
            Companion.NoteFearSighted();
            Companion.Speak(SpeechContext.FearSighted, name, urgent: true);
            Note($"balked at {target.Name}: afraid of {Companion.FearName}");
        }

        if (now - _hesitateUntil < 0)
        {
            return FindUnfearedThreat();
        }

        _steeled = true;

        if (Utility.RandomBool())
        {
            Companion.Speak(SpeechContext.FearSteeled, name, urgent: true);
        }

        Note($"steeled themselves and took on {target.Name}");
        return target;
    }

    private Mobile FindUnfearedThreat()
    {
        Mobile best = null;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < _threats.Count; i++)
        {
            var threat = _threats[i];

            if (Companion.Fears(threat))
            {
                continue;
            }

            var distance = Mobile.GetDistanceToSqrt(threat);

            if (distance <= ThreatRange && distance < bestDistance && Mobile.InLOS(threat))
            {
                best = threat;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void ResetFear()
    {
        _hesitateUntil = 0;
        _steeled = false;
    }
}
