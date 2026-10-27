using System;
using System.Collections.Generic;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Commands;

public static class RemoveAnachronisms
{
    // Creatures from after Third Dawn: ML/SE, AOS, the Solen and Jwilson. LBR is deliberately kept.
    private static readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mondain's Legacy / Samurai Empire
        "Abscess", "Coil", "Gnaw", "Grobu", "Guile", "Irk", "Lurg", "Malefic", "Spite", "Swoop", "Thrasher",
        "Putrefier", "Virulent", "InsaneDryad", "InterredGrizzle", "Changeling", "CuSidhe", "Satyr",
        "CrystalDaemon", "CrystalWisp", "CrystalLatticeSeeker", "CrystalSeaSerpent", "CrystalVortex", "CrystalHydra",
        "LadySabrix", "Szavetra", "Chiikkaha", "MougGuur", "Saliva", "Silk", "Ferret", "Hydra", "Kirin", "IharaSoko",

        // Age of Shadows
        "PlagueBeast", "PlagueBeastLord", "PlagueSpawn", "ArcaneDaemon", "Bogling", "BogThing", "Quagmire",
        "SwampTentacle",

        // 2002 publishes
        "BlackSolenInfiltratorQueen", "BlackSolenInfiltratorWarrior", "RedSolenInfiltratorQueen",
        "RedSolenInfiltratorWarrior", "Jwilson"
    };

    public static void Configure()
    {
        CommandSystem.Register("RemoveAnachronisms", AccessLevel.Administrator, RemoveAnachronisms_OnCommand);
    }

    [Usage("RemoveAnachronisms")]
    [Description(
        "Deletes creatures from after Third Dawn (ML, SE, AOS, Solen, Jwilson) and strips them from all spawners. " +
        "Players and pets are left alone."
    )]
    public static void RemoveAnachronisms_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var cleanup = Collect();

        if (cleanup.Total == 0)
        {
            from.SendMessage("There are no creatures from after Third Dawn left in the world.");
            return;
        }

        from.SendGump(new RemoveAnachronismsNoticeGump(cleanup, okay => Remove_Callback(from, okay, cleanup)));
    }

    private static Cleanup Collect()
    {
        var cleanup = new Cleanup();

        // One-off admin sweep, same approach as [ClearAll.
        foreach (var item in World.Items.Values)
        {
            if (item is not BaseSpawner spawner)
            {
                continue;
            }

            List<SpawnerEntry> lateEntries = null;

            foreach (var entry in spawner.Entries)
            {
                if (_names.Contains(entry.SpawnedName))
                {
                    (lateEntries ??= []).Add(entry);
                }
            }

            if (lateEntries == null)
            {
                continue;
            }

            if (lateEntries.Count == spawner.Entries.Count)
            {
                cleanup.Spawners.Add(spawner);
            }
            else
            {
                cleanup.Entries.Add((spawner, lateEntries));
            }
        }

        foreach (var mob in World.Mobiles.Values)
        {
            if (mob.Player || mob is BaseCreature { Controlled: true } || !_names.Contains(mob.GetType().Name))
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
            from.SendMessage("You have chosen not to remove the late-era creatures.");
            return;
        }

        CommandLogging.WriteLine(from, $"{from.AccessLevel} {CommandLogging.Format(from)} removing anachronisms: {cleanup}");

        NetState.FlushAll();

        // Spawners first, so nothing respawns while the creatures are being deleted.
        var entryCount = 0;

        foreach (var (spawner, entries) in cleanup.Entries)
        {
            if (spawner.Deleted)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                spawner.RemoveEntry(entry);
                entryCount++;
            }
        }

        foreach (var spawner in cleanup.Spawners)
        {
            spawner.Delete();
        }

        foreach (var mob in cleanup.Mobiles)
        {
            mob.Delete();
        }

        from.SendMessage(
            $"Removed {cleanup.Mobiles.Count} creatures, {cleanup.Spawners.Count} spawners and {entryCount} spawner entries."
        );
    }

    private class Cleanup
    {
        public readonly List<Mobile> Mobiles = [];
        public readonly List<BaseSpawner> Spawners = [];
        public readonly List<(BaseSpawner Spawner, List<SpawnerEntry> Entries)> Entries = [];

        public int Total => Mobiles.Count + Spawners.Count + Entries.Count;

        public override string ToString() =>
            $"{Mobiles.Count} creatures, {Spawners.Count} spawners, {Entries.Count} mixed spawners";
    }

    private class RemoveAnachronismsNoticeGump : StaticWarningGump<RemoveAnachronismsNoticeGump>
    {
        public override int Width => 360;
        public override int Height => 260;

        public override string Content { get; }

        public RemoveAnachronismsNoticeGump(Cleanup cleanup, Action<bool> callback) : base(callback) =>
            Content =
                $"You are about to delete {cleanup.Mobiles.Count} creatures from after Third Dawn and " +
                $"{cleanup.Spawners.Count} spawners, and strip them from {cleanup.Entries.Count} other spawners. " +
                "Do you really wish to continue?";
    }
}
