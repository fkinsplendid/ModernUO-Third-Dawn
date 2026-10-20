using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

/// <summary>
/// As in Third Dawn, magic weapon accuracy and armor protection work without showing: the client is
/// told the skill and armor rating without them.
/// </summary>
[Collection("Sequential UOContent Tests")]
public class HiddenMagicBonusTests
{
    private static PlayerMobile CreatePlayer()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.RawStr = 100;
        player.RawDex = 100;
        World.AddEntity(player);
        player.MoveToWorld(new Point3D(5300, 3700, 0), Map.Felucca);
        return player;
    }

    [Fact]
    public void WeaponAccuracy_CountsInChecks_ButIsNotShown()
    {
        var expansion = Core.Expansion;
        Core.Expansion = Expansion.UOTD;
        var player = CreatePlayer();
        var katana = new Katana { AccuracyLevel = WeaponAccuracyLevel.Supremely, Layer = Layer.OneHanded };

        try
        {
            player.Skills[SkillName.Tactics].Base = 80.0;
            Assert.True(player.EquipItem(katana));

            Assert.Equal(105.0, player.Skills[SkillName.Tactics].NonRacialValue, 1);
            Assert.Equal(80.0, player.Skills[SkillName.Tactics].DisplayValue, 1);
        }
        finally
        {
            katana.Delete();
            player.Delete();
            Core.Expansion = expansion;
        }
    }

    [Fact]
    public void ArmorProtection_Protects_ButIsNotShown()
    {
        var expansion = Core.Expansion;
        Core.Expansion = Expansion.UOTD;
        var player = CreatePlayer();
        var plain = new PlateChest { Layer = Layer.InnerTorso };
        var magic = new PlateChest { ProtectionLevel = ArmorProtectionLevel.Invulnerability, Layer = Layer.InnerTorso };

        try
        {
            Assert.True(player.EquipItem(plain));
            var plainRating = player.ArmorRating;
            plain.Delete();

            Assert.True(player.EquipItem(magic));

            Assert.True(player.ArmorRating > plainRating);
            Assert.Equal(plainRating, player.DisplayArmorRating, 3);
        }
        finally
        {
            plain.Delete();
            magic.Delete();
            player.Delete();
            Core.Expansion = expansion;
        }
    }
}
