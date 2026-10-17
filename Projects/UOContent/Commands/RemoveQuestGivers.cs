using System;
using System.Collections.Generic;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Commands;

public static class RemoveQuestGivers
{
    public static void Configure()
    {
        CommandSystem.Register("RemoveQuestGivers", AccessLevel.Administrator, RemoveQuestGivers_OnCommand);
    }

    [Usage("RemoveQuestGivers")]
    [Description(
        "Deletes every quest giver, escort NPC and quest item placed in the world, and strips them from all spawners. " +
        "Players, pets and anything inside containers or houses are left alone."
    )]
    public static void RemoveQuestGivers_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var cleanup = Collect();

        if (cleanup.Total == 0)
        {
            from.SendMessage("There are no quest givers, quest items or quest spawners left in the world.");
            return;
        }

        from.SendGump(new RemoveQuestGiversNoticeGump(cleanup, okay => Remove_Callback(from, okay, cleanup)));
    }

    // Quest NPCs and quest props all live under these namespaces; escorts are plain townsfolk.
    private static bool IsQuestType(Type type)
    {
        var ns = type.Namespace;

        return ns != null && (ns.StartsWith("Server.Engines.Quests", StringComparison.Ordinal) ||
                              ns.StartsWith("Server.Engines.MLQuests", StringComparison.Ordinal));
    }

    private static bool IsQuestSpawn(Type type) =>
        type != null && (typeof(BaseEscortable).IsAssignableFrom(type) || IsQuestType(type));

    private static Cleanup Collect()
    {
        var cleanup = new Cleanup();

        // One-off admin sweep, same approach as [ClearAll.
        foreach (var item in World.Items.Values)
        {
            if (item is BaseSpawner spawner)
            {
                List<SpawnerEntry> questEntries = null;

                foreach (var entry in spawner.Entries)
                {
                    if (IsQuestSpawn(AssemblyHandler.FindTypeByName(entry.SpawnedName)))
                    {
                        (questEntries ??= []).Add(entry);
                    }
                }

                if (questEntries == null)
                {
                    continue;
                }

                if (questEntries.Count == spawner.Entries.Count)
                {
                    cleanup.Spawners.Add(spawner);
                }
                else
                {
                    cleanup.Entries.Add((spawner, questEntries));
                }
            }
            else if (item.Parent == null && IsQuestType(item.GetType()) && BaseHouse.FindHouseAt(item) == null)
            {
                cleanup.Items.Add(item);
            }
        }

        foreach (var mob in World.Mobiles.Values)
        {
            if (mob.Player || mob is BaseCreature { Controlled: true } || !IsQuestSpawn(mob.GetType()))
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
            from.SendMessage("You have chosen not to remove the quest givers.");
            return;
        }

        CommandLogging.WriteLine(from, $"{from.AccessLevel} {CommandLogging.Format(from)} removing quest givers: {cleanup}");

        NetState.FlushAll();

        // Spawners first, so nothing respawns while the NPCs are being deleted.
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

        foreach (var item in cleanup.Items)
        {
            item.Delete();
        }

        foreach (var mob in cleanup.Mobiles)
        {
            mob.Delete();
        }

        from.SendMessage(
            $"Removed {cleanup.Mobiles.Count} NPCs, {cleanup.Items.Count} quest items, " +
            $"{cleanup.Spawners.Count} spawners and {entryCount} spawner entries."
        );
    }

    private class Cleanup
    {
        public readonly List<Mobile> Mobiles = [];
        public readonly List<Item> Items = [];
        public readonly List<BaseSpawner> Spawners = [];
        public readonly List<(BaseSpawner Spawner, List<SpawnerEntry> Entries)> Entries = [];

        public int Total => Mobiles.Count + Items.Count + Spawners.Count + Entries.Count;

        public override string ToString() =>
            $"{Mobiles.Count} NPCs, {Items.Count} quest items, {Spawners.Count} spawners, {Entries.Count} mixed spawners";
    }

    private class RemoveQuestGiversNoticeGump : StaticWarningGump<RemoveQuestGiversNoticeGump>
    {
        public override int Width => 360;
        public override int Height => 260;

        public override string Content { get; }

        public RemoveQuestGiversNoticeGump(Cleanup cleanup, Action<bool> callback) : base(callback) =>
            Content =
                $"You are about to delete {cleanup.Mobiles.Count} quest NPCs, {cleanup.Items.Count} quest items and " +
                $"{cleanup.Spawners.Count} quest spawners, and strip quest givers from {cleanup.Entries.Count} other spawners. " +
                "Do you really wish to continue?";
    }
}
