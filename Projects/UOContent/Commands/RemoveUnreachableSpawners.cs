using System;
using System.Collections.Generic;
using Server.Engines.Companions;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Commands;

public static class RemoveUnreachableSpawners
{
    // The Third Dawn map ends here; the strip east of it is Mondain's Legacy land with no way in.
    private const int EastStripStart = 6144;

    public static void Configure()
    {
        CommandSystem.Register("RemoveUnreachableSpawners", AccessLevel.Administrator, RemoveUnreachableSpawners_OnCommand);
    }

    [Usage("RemoveUnreachableSpawners")]
    [Description(
        "Trammel-only shard: deletes every spawner in Felucca and in Trammel's east strip (x 6144 and up), along with " +
        "the creatures and NPCs living there. Players, pets, companions and anything in a house are left alone."
    )]
    public static void RemoveUnreachableSpawners_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var cleanup = Collect();

        if (cleanup.Total == 0)
        {
            from.SendMessage("There are no spawners or creatures left in Felucca or the east strip.");
            return;
        }

        from.SendGump(new RemoveUnreachableSpawnersNoticeGump(cleanup, okay => Remove_Callback(from, okay, cleanup)));
    }

    private static bool IsUnreachable(Map map, Point3D loc) =>
        map == Map.Felucca || map == Map.Trammel && loc.X >= EastStripStart;

    private static Cleanup Collect()
    {
        var cleanup = new Cleanup();

        // One-off admin sweep, same approach as [ClearAll.
        foreach (var item in World.Items.Values)
        {
            if (item is BaseSpawner { Parent: null } spawner && IsUnreachable(spawner.Map, spawner.Location))
            {
                cleanup.Spawners.Add(spawner);
            }
        }

        foreach (var mob in World.Mobiles.Values)
        {
            if (!IsUnreachable(mob.Map, mob.Location) || mob.Player ||
                mob is BaseCompanion or PlayerVendor or PlayerBarkeeper ||
                mob is BaseCreature { Controlled: true } or BaseCreature { Summoned: true } ||
                BaseHouse.FindHouseAt(mob) != null)
            {
                continue;
            }

            cleanup.Mobiles.Add(mob);
        }

        return cleanup;
    }

    private static void Remove_Callback(Mobile from, bool okay, Cleanup cleanup)
    {
        if (!okay)
        {
            from.SendMessage("You have chosen not to remove the unreachable spawners.");
            return;
        }

        CommandLogging.WriteLine(from, $"{from.AccessLevel} {CommandLogging.Format(from)} removing unreachable spawners: {cleanup}");

        NetState.FlushAll();

        // Spawners first, so nothing respawns while the creatures are being deleted.
        foreach (var spawner in cleanup.Spawners)
        {
            spawner.Delete();
        }

        var mobileCount = 0;

        foreach (var mob in cleanup.Mobiles)
        {
            // Deleting a spawner already took its own spawns.
            if (!mob.Deleted)
            {
                mob.Delete();
                mobileCount++;
            }
        }

        from.SendMessage(
            $"Removed {cleanup.Spawners.Count} spawners and {mobileCount} more creatures and NPCs " +
            "from Felucca and the east strip."
        );
    }

    private class Cleanup
    {
        public readonly List<Mobile> Mobiles = [];
        public readonly List<BaseSpawner> Spawners = [];

        public int Total => Mobiles.Count + Spawners.Count;

        public override string ToString() => $"{Spawners.Count} spawners, {Mobiles.Count} creatures and NPCs";
    }

    private class RemoveUnreachableSpawnersNoticeGump : StaticWarningGump<RemoveUnreachableSpawnersNoticeGump>
    {
        public override int Width => 360;
        public override int Height => 260;

        public override string Content { get; }

        public RemoveUnreachableSpawnersNoticeGump(Cleanup cleanup, Action<bool> callback) : base(callback) =>
            Content =
                $"You are about to delete {cleanup.Spawners.Count} spawners and {cleanup.Mobiles.Count} creatures and " +
                "NPCs in Felucca and Trammel's east strip. Players, pets, companions and anything in a house are " +
                "left alone. Do you really wish to continue?";
    }
}
