using Server.Gumps;
using Server.Network;

namespace Server.Engines.Companions;

/// <summary>
/// One row per hired companion: level, experience, health, wages and where they are, with a
/// button for their skills.
/// </summary>
public class CompanionStatusGump : DynamicGump
{
    private const int Width = 520;
    private const int RowHeight = 90;

    private readonly Mobile _employer;

    public CompanionStatusGump(Mobile employer) : base(60, 60) => _employer = employer;

    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var party = CompanionSystem.GetParty(_employer);
        var rows = 0;

        if (party != null)
        {
            foreach (var c in party.Members)
            {
                if (c != null)
                {
                    rows++;
                }
            }
        }

        var height = 60 + (rows == 0 ? 30 : rows * RowHeight);

        builder.AddPage();
        builder.AddBackground(0, 0, Width, height, 9270);
        builder.AddAlphaRegion(10, 10, Width - 20, height - 20);
        builder.AddHtml(20, 18, Width - 40, 20, "Your Companions", "#FFC000", 4, align: TextAlignment.Center);

        if (rows == 0)
        {
            builder.AddHtml(20, 50, Width - 40, 20, "You have no companions. Visit a companion guildmaster to hire one.", "#FFFFFF");
            return;
        }

        var y = 45;

        foreach (var c in party.Members)
        {
            if (c == null)
            {
                continue;
            }

            var where = c.State switch
            {
                CompanionState.Resting => "At the guild",
                CompanionState.Waiting => "Waiting for you",
                _ when c.IsDeadPet     => "Fallen",
                _                      => "With you"
            };

            var xp = c.ExperienceToNextLevel > 0 ? $"{c.Experience * 100 / c.ExperienceToNextLevel}%" : "max";
            var days = c.DailyWage > 0 ? c.Purse / (double)c.DailyWage : 0;

            builder.AddButton(20, y, 4011, 4013, 1 + (int)c.Role);
            builder.AddHtml(55, y, 265, 20, $"{c.Name} {c.Title}", "#FFFFFF", 4);
            builder.AddHtml(330, y, Width - 350, 20, where, c.IsDeadPet ? "#FF4040" : "#80FF80", 4, align: TextAlignment.Right);

            builder.AddHtml(20, y + 20, 250, 20, $"Level {c.Level}  (next: {xp})   Kills: {c.Kills}", "#C0C0C0");
            builder.AddHtml(270, y + 20, 230, 20, $"HP {c.Hits}/{c.HitsMax}   Mana {c.Mana}/{c.ManaMax}", "#C0C0C0");
            builder.AddHtml(
                20,
                y + 40,
                Width - 40,
                20,
                $"Purse {c.Purse} gold  ·  Wage {c.DailyWage} per day  ·  Covers {days:0.#} days  ·  Next wage in {c.MinutesUntilWage} min",
                "#C0C0C0"
            );

            var owed = c.Owed > 0 ? $"  ·  Owed {c.Owed} gold" : "";

            builder.AddHtml(
                20,
                y + 60,
                Width - 40,
                20,
                $"Spirits: {MoodText(c.Mood)}  ·  {WearinessText(c)}{owed}",
                c.Mood <= CompanionMood.Unhappy ? "#FF8080" : "#C0C0C0"
            );

            y += RowHeight;
        }
    }

    private static string MoodText(CompanionMood mood) =>
        mood switch
        {
            CompanionMood.Bitter  => "Bitter",
            CompanionMood.Unhappy => "Unhappy",
            CompanionMood.Content => "Content",
            CompanionMood.Loyal   => "Loyal",
            _                     => "Devoted"
        };

    private static string WearinessText(BaseCompanion c) =>
        c.IsExhausted ? "Exhausted" : c.IsTired ? "Tired" : "Rested";

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var role = (CompanionRole)(info.ButtonID - 1);

        if (info.ButtonID == 0 || role > CompanionRole.Mage || _employer.Deleted)
        {
            return;
        }

        if (CompanionSystem.GetCompanion(_employer, role) is { } companion)
        {
            _employer.SendGump(new CompanionSkillsGump(_employer, companion));
        }
    }
}
