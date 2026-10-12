using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Engines.MLQuests;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Peasant : BaseEscortable
{
    private static readonly string[] _ambientSpeech =
    [
        "Fine day, isn't it?",
        "My back aches something terrible.",
        "Hard times, hard times...",
        "Watch where you're stepping!",
        "The market's been quiet today.",
        "I've been on my feet all day.",
        "What brings you to these parts?",
        "Careful on the road. I heard tales of bandits.",
        "The guards have been scarce of late.",
        "I'm just trying to get by, is all.",
        "Did you see that dragon fly overhead last night?",
        "Stay out of the dungeons, I say. Nothing good comes of it.",
        "Have you heard what's been happening out east?",
        "I wonder if there's a storm coming.",
        "These boots are going to be the death of me.",
    ];

    private TimerExecutionToken _speechToken;

    [Constructible]
    public Peasant()
    {
        Title = "the peasant";
        StartSpeechTimer();
    }

    private void StartSpeechTimer()
    {
        Timer.StartTimer(
            TimeSpan.FromSeconds(Utility.RandomMinMax(15, 45)),
            TimeSpan.FromSeconds(30),
            OnAmbientSpeech,
            out _speechToken
        );
    }

    private void OnAmbientSpeech()
    {
        if (Deleted || !Alive || Controlled || Map == null || Map == Map.Internal)
        {
            return;
        }

        foreach (var m in Map.GetMobilesInRange(Location, 8))
        {
            if (m is PlayerMobile)
            {
                Say(_ambientSpeech.RandomElement());
                return;
            }
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization() => StartSpeechTimer();

    public override void OnAfterDelete()
    {
        _speechToken.Cancel();
        base.OnAfterDelete();
    }

    protected override List<MLQuest> ConstructQuestList() => null;

    public override void Shout(PlayerMobile pm) { }

    public override bool CanTeach => true;
    public override bool ClickTitle => false; // Do not display 'the peasant' when single-clicking

    private static int GetRandomHue()
    {
        return Utility.Random(6) switch
        {
            0 => 0,
            1 => Utility.RandomBlueHue(),
            2 => Utility.RandomGreenHue(),
            3 => Utility.RandomRedHue(),
            4 => Utility.RandomYellowHue(),
            5 => Utility.RandomNeutralHue(),
            _ => 0
        };
    }

    public override void InitOutfit()
    {
        if (Female)
        {
            AddItem(new PlainDress());
        }
        else
        {
            AddItem(new Shirt(GetRandomHue()));
        }

        var lowHue = GetRandomHue();

        AddItem(new ShortPants(lowHue));

        if (Female)
        {
            AddItem(new Boots(lowHue));
        }
        else
        {
            AddItem(new Shoes(lowHue));
        }

        // if (!Female)
        // AddItem( new BodySash( lowHue ) );

        // AddItem( new Cloak( GetRandomHue() ) );

        // if (!Female)
        // AddItem( new Longsword() );

        Utility.AssignRandomHair(this);

        PackGold(200, 250);
    }
}
