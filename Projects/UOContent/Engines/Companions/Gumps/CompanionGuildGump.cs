using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.Companions;

/// <summary>
/// The guildmaster's ledger: hire a companion for each role, bring back one who is resting at the
/// guild, pay for a fallen companion's resurrection, or end a contract for good.
/// </summary>
public class CompanionGuildGump : DynamicGump
{
    private const int Width = 500;
    private const int RowHeight = 60;

    private static readonly CompanionRole[] _roles = [CompanionRole.Warrior, CompanionRole.Bard, CompanionRole.Mage];

    private readonly Mobile _player;
    private readonly Mobile _guildmaster;
    private readonly CompanionRole? _confirmTerminate;

    public CompanionGuildGump(Mobile player, Mobile guildmaster, CompanionRole? confirmTerminate = null) : base(80, 80)
    {
        _player = player;
        _guildmaster = guildmaster;
        _confirmTerminate = confirmTerminate;
    }

    public override bool Singleton => true;

    private enum Action
    {
        Hire = 1,
        Rehire,
        Resurrect,
        Terminate,
        ConfirmTerminate
    }

    private static int ButtonId(Action action, CompanionRole role) => (int)action * 10 + (int)role;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var height = 90 + _roles.Length * RowHeight;

        builder.AddPage();
        builder.AddBackground(0, 0, Width, height, 9270);
        builder.AddAlphaRegion(10, 10, Width - 20, height - 20);
        builder.AddHtml(20, 18, Width - 40, 20, "Companion Guild", "#FFC000", 5, align: TextAlignment.Center);

        if (_confirmTerminate is { } confirmRole)
        {
            var c = CompanionSystem.GetCompanion(_player, confirmRole);

            builder.AddHtml(
                20,
                60,
                Width - 40,
                60,
                $"End {c?.Name}'s contract for good? They leave the guild and their experience is lost. Anything of yours they carry goes to your bank box.",
                "#FF8080",
                4
            );
            builder.AddButton(40, 130, 4005, 4007, ButtonId(Action.ConfirmTerminate, confirmRole));
            builder.AddHtml(75, 132, 150, 20, "Yes, end it", "#FFFFFF");
            builder.AddButton(260, 130, 4017, 4019, 0);
            builder.AddHtml(295, 132, 150, 20, "No, keep them", "#FFFFFF");
            return;
        }

        builder.AddHtml(
            20,
            42,
            Width - 40,
            20,
            $"Hire fee {CompanionSettings.HireFee} gold, includes the first day's wage.",
            "#C0C0C0",
            align: TextAlignment.Center
        );

        var y = 75;

        foreach (var role in _roles)
        {
            var c = CompanionSystem.GetCompanion(_player, role);
            var title = role switch
            {
                CompanionRole.Warrior => "Warrior",
                CompanionRole.Bard    => "Bard",
                _                     => "Mage"
            };

            if (c == null)
            {
                builder.AddHtml(20, y, 300, 20, title, "#FFFFFF", 4);
                builder.AddHtml(20, y + 20, 300, 20, "No one under contract.", "#C0C0C0");
                builder.AddButton(340, y + 5, 4005, 4007, ButtonId(Action.Hire, role));
                builder.AddHtml(375, y + 7, 110, 20, "Hire", "#FFFFFF");
            }
            else
            {
                builder.AddHtml(20, y, 320, 20, $"{title}: {c.Name}, level {c.Level}", "#FFFFFF", 4);

                if (c.State == CompanionState.Resting)
                {
                    var cost = RehireCost(c);
                    builder.AddHtml(20, y + 20, 320, 20, $"Resting at the guild. Rehire: {cost} gold.", "#C0C0C0");
                    builder.AddButton(340, y + 5, 4005, 4007, ButtonId(Action.Rehire, role));
                    builder.AddHtml(375, y + 7, 110, 20, "Rehire", "#FFFFFF");
                    builder.AddButton(340, y + 30, 4017, 4019, ButtonId(Action.Terminate, role));
                    builder.AddHtml(375, y + 32, 110, 20, "End contract", "#FF8080");
                }
                else if (c.IsDeadPet)
                {
                    builder.AddHtml(20, y + 20, 320, 20, $"Fallen. Resurrection: {CompanionSettings.ResurrectFee} gold.", "#FF8080");
                    builder.AddButton(340, y + 5, 4005, 4007, ButtonId(Action.Resurrect, role));
                    builder.AddHtml(375, y + 7, 110, 20, "Resurrect", "#FFFFFF");
                }
                else
                {
                    builder.AddHtml(20, y + 20, 320, 20, "In your service.", "#80FF80");
                }
            }

            y += RowHeight;
        }
    }

    private static int RehireCost(BaseCompanion c) =>
        c.DailyWage + (c.IsDeadPet ? CompanionSettings.ResurrectFee : 0);

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var buttonId = info.ButtonID;

        if (buttonId == 0 || _player.Deleted || !_player.Alive || !_player.InRange(_guildmaster, 8))
        {
            return;
        }

        var action = (Action)(buttonId / 10);
        var role = (CompanionRole)(buttonId % 10);

        if (role > CompanionRole.Mage)
        {
            return;
        }

        var companion = CompanionSystem.GetCompanion(_player, role);

        switch (action)
        {
            case Action.Hire when companion == null:
                {
                    Hire(role);
                    break;
                }
            case Action.Rehire when companion?.State == CompanionState.Resting:
                {
                    Rehire(companion);
                    break;
                }
            case Action.Resurrect when companion?.IsDeadPet == true && companion.State != CompanionState.Resting:
                {
                    Resurrect(companion);
                    break;
                }
            case Action.Terminate when companion?.State == CompanionState.Resting:
                {
                    _player.SendGump(new CompanionGuildGump(_player, _guildmaster, role));
                    return;
                }
            case Action.ConfirmTerminate when companion?.State == CompanionState.Resting:
                {
                    var name = companion.Name;
                    var returned = companion.EndContract();

                    _guildmaster.SayTo(
                        _player,
                        returned > 0
                            ? $"Very well. {name}'s contract is ended. Their belongings are in your bank box."
                            : $"Very well. {name}'s contract is ended."
                    );
                    break;
                }
        }

        _player.SendGump(new CompanionGuildGump(_player, _guildmaster));
    }

    private bool HasFollowerRoom()
    {
        if (_player.Followers + 1 <= _player.FollowersMax)
        {
            return true;
        }

        _guildmaster.SayTo(_player, "You have too many followers already.");
        return false;
    }

    private void Hire(CompanionRole role)
    {
        if (!HasFollowerRoom() || !TryCharge(CompanionSettings.HireFee))
        {
            return;
        }

        BaseCompanion companion = role switch
        {
            CompanionRole.Warrior => new CompanionWarrior(),
            CompanionRole.Bard    => new CompanionBard(),
            _                     => new CompanionMage()
        };

        if (!companion.BeginService(_player))
        {
            companion.Delete();
            Refund(CompanionSettings.HireFee);
        }
    }

    private void Rehire(BaseCompanion companion)
    {
        if (!HasFollowerRoom() || !TryCharge(RehireCost(companion)))
        {
            return;
        }

        if (companion.IsDeadPet)
        {
            companion.ResurrectPet();
        }

        if (!companion.ResumeService())
        {
            Refund(RehireCost(companion));
        }
    }

    private void Resurrect(BaseCompanion companion)
    {
        if (!companion.InRange(_guildmaster, 8))
        {
            _guildmaster.SayTo(_player, "Bring their spirit here and I'll see what can be done.");
            return;
        }

        if (TryCharge(CompanionSettings.ResurrectFee))
        {
            _guildmaster.Say("Return to the living!");
            companion.PlaySound(0x214);
            companion.FixedEffect(0x376A, 10, 16);
            companion.ResurrectPet();
        }
    }

    /// <summary>Gives the money back: to the bank, or into the player's hands if the bank is full.</summary>
    private void Refund(int amount)
    {
        if (!Banker.Deposit(_player, amount))
        {
            _player.AddToBackpack(new Gold(amount));
        }

        _guildmaster.SayTo(_player, "Something went wrong. Your gold has been returned.");
    }

    /// <summary>Takes gold from the backpack, or from the bank if the backpack is short.</summary>
    private bool TryCharge(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (_player.Backpack?.ConsumeTotal(typeof(Gold), amount) == true || Banker.Withdraw(_player, amount))
        {
            _player.SendMessage($"You pay {amount} gold.");
            return true;
        }

        _guildmaster.SayTo(_player, $"That will be {amount} gold, and you don't have it.");
        return false;
    }
}
