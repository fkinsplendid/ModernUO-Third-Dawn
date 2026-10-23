using System;
using Server;
using Server.Engines.Companions;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MasterBlacksmithTests : IDisposable
{
    private readonly Expansion _expansion;

    // Felucca, clear of the other test classes' coordinates.
    private static readonly Point3D Origin = new(5300, 3900, 0);

    public MasterBlacksmithTests()
    {
        _expansion = Core.Expansion;
        Core.Expansion = Expansion.UOTD;

        CompanionSettings.Configure();
        DefBlacksmithy.Initialize();

        NPCSpeeds.RegisterSpeed(
            new NPCSpeeds.SpeedClassEntry { Level = SpeedLevel.Medium, ActiveSpeed = 0.2, PassiveSpeed = 0.4, Types = [] }
        );

        Timer.Init(Core._tickCount);
    }

    public void Dispose() => Core.Expansion = _expansion;

    private static void Advance(TimeSpan time)
    {
        var end = Core._tickCount + (long)time.TotalMilliseconds;

        while (Core._tickCount < end)
        {
            Core._tickCount += 8;
            Timer.Slice(Core._tickCount);
        }
    }

    private static PlayerMobile CreatePlayer(int gold)
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        World.AddEntity(player);

        if (player.Backpack == null)
        {
            player.AddItem(new Backpack());
        }

        if (gold > 0)
        {
            player.Backpack.DropItem(new Gold(gold));
        }

        player.MoveToWorld(Origin, Map.Felucca);
        return player;
    }

    private static MasterBlacksmith CreateSmith()
    {
        var smith = new MasterBlacksmith();
        smith.MoveToWorld(new Point3D(Origin.X + 1, Origin.Y, Origin.Z), Map.Felucca);
        return smith;
    }

    // Lightly worn: a GM always manages this, so the outcome doesn't depend on the dice.
    private static Longsword CreateWornSword()
    {
        var sword = new Longsword { Layer = Layer.OneHanded };
        sword.MaxHitPoints = 40;
        sword.HitPoints = 35;
        return sword;
    }

    private static void SayTo(Mobile listener, Mobile speaker, string text) =>
        listener.OnSpeech(new SpeechEventArgs(speaker, text, MessageType.Regular, 0x3B2, []));

    [Fact]
    public void Player_Agrees_PaysAndGetsItRepaired()
    {
        var player = CreatePlayer(1000);
        var smith = CreateSmith();
        var sword = CreateWornSword();
        player.Backpack.DropItem(sword);

        try
        {
            Assert.True(smith.OnDragDrop(player, sword));
            Assert.True(smith.IsBusy);
            Assert.NotSame(player.Backpack, sword.Parent);

            Advance(TimeSpan.FromSeconds(3));
            SayTo(smith, player, "yes please");
            Advance(TimeSpan.FromSeconds(5));

            Assert.False(smith.IsBusy);
            Assert.Same(player.Backpack, sword.Parent);
            Assert.Equal(sword.MaxHitPoints, sword.HitPoints);
            Assert.Equal(1000 - (10 + 5 * 5), player.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            sword.Delete();
            smith.Delete();
            player.Delete();
        }
    }

    [Fact]
    public void Player_HandsOverGold_PaysTheQuote_AndGetsChange()
    {
        var player = CreatePlayer(0);
        var smith = CreateSmith();
        var sword = CreateWornSword();
        player.Backpack.DropItem(sword);
        var coins = new Gold(100);

        try
        {
            // Gold before there's a job is refused, not pocketed.
            player.Backpack.DropItem(coins);
            Assert.False(smith.OnDragDrop(player, coins));

            smith.OnDragDrop(player, sword);
            Advance(TimeSpan.FromSeconds(3));

            // Short of the quote: refused.
            var few = new Gold(5);
            player.Backpack.DropItem(few);
            Assert.False(smith.OnDragDrop(player, few));

            Assert.True(smith.OnDragDrop(player, coins));
            Advance(TimeSpan.FromSeconds(5));

            Assert.False(smith.IsBusy);
            Assert.Same(player.Backpack, sword.Parent);
            Assert.Equal(sword.MaxHitPoints, sword.HitPoints);
            // The change (and the refused 5) are back in the pack.
            Assert.Equal(5 + 100 - (10 + 5 * 5), player.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            sword.Delete();
            coins.Delete();
            smith.Delete();
            player.Delete();
        }
    }

    [Fact]
    public void Player_Declines_GetsItBackUnchangedAndFree()
    {
        var player = CreatePlayer(1000);
        var smith = CreateSmith();
        var sword = CreateWornSword();
        player.Backpack.DropItem(sword);

        try
        {
            smith.OnDragDrop(player, sword);
            Advance(TimeSpan.FromSeconds(3));
            SayTo(smith, player, "no, I'll keep it");

            Assert.False(smith.IsBusy);
            Assert.Same(player.Backpack, sword.Parent);
            Assert.Equal(35, sword.HitPoints);
            Assert.Equal(1000, player.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            sword.Delete();
            smith.Delete();
            player.Delete();
        }
    }

    [Fact]
    public void NoAnswer_HandsItBack()
    {
        var player = CreatePlayer(1000);
        var smith = CreateSmith();
        var sword = CreateWornSword();
        player.Backpack.DropItem(sword);

        try
        {
            smith.OnDragDrop(player, sword);
            Advance(TimeSpan.FromSeconds(65));

            Assert.False(smith.IsBusy);
            Assert.Same(player.Backpack, sword.Parent);
            Assert.Equal(1000, player.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            sword.Delete();
            smith.Delete();
            player.Delete();
        }
    }

    [Fact]
    public void Refuses_ItemsThatArentMetalwork()
    {
        var player = CreatePlayer(0);
        var smith = CreateSmith();
        var tunic = new LeatherChest();
        player.Backpack.DropItem(tunic);

        try
        {
            Assert.False(MasterBlacksmith.IsMetalwork(tunic));
            Assert.False(smith.OnDragDrop(player, tunic));
            Assert.False(smith.IsBusy);
            Assert.Same(player.Backpack, tunic.Parent);
        }
        finally
        {
            tunic.Delete();
            smith.Delete();
            player.Delete();
        }
    }

    [Fact]
    public void Companion_HandsOverGear_EmployerPays_AndItIsWornAgain()
    {
        var employer = CreatePlayer(1000);
        var smith = CreateSmith();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);

            var sword = CreateWornSword();
            companion.FindItemOnLayer(Layer.OneHanded)?.Delete();
            Assert.True(companion.EquipItem(sword));

            Assert.True(smith.OnDragDrop(companion, sword));
            Advance(TimeSpan.FromSeconds(3));

            // The companion can't agree to the bill; their employer must.
            SayTo(smith, companion, "yes");
            Assert.True(smith.IsBusy);

            SayTo(smith, employer, "aye");
            Advance(TimeSpan.FromSeconds(5));

            Assert.False(smith.IsBusy);
            Assert.Same(companion, sword.Parent);
            Assert.Equal(sword.MaxHitPoints, sword.HitPoints);
            Assert.Equal(1000 - (10 + 5 * 5), employer.Backpack.GetAmount(typeof(Gold)));

            sword.Delete();
        }
        finally
        {
            companion.Delete();
            smith.Delete();
            employer.Delete();
        }
    }

    [Fact]
    public void Companion_BillDeclined_GetsGearBack()
    {
        var employer = CreatePlayer(1000);
        var smith = CreateSmith();
        var companion = new CompanionWarrior();

        try
        {
            companion.BeginService(employer);
            companion.FindItemOnLayer(Layer.OneHanded)?.Delete();

            var sword = CreateWornSword();
            Assert.True(companion.EquipItem(sword));

            smith.OnDragDrop(companion, sword);
            Advance(TimeSpan.FromSeconds(3));
            SayTo(smith, employer, "no");

            Assert.False(smith.IsBusy);
            Assert.Same(companion, sword.Parent);
            Assert.Equal(35, sword.HitPoints);
            Assert.Equal(1000, employer.Backpack.GetAmount(typeof(Gold)));

            sword.Delete();
        }
        finally
        {
            companion.Delete();
            smith.Delete();
            employer.Delete();
        }
    }
}
