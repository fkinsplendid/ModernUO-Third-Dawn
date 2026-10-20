using System;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.VeteranRewards
{
    public static class RewardSystem
    {
        private static RewardCategory[] m_Categories;
        private static RewardList[] m_Lists;

        public static bool Enabled { get; private set; }

        // assuming vet rewards are enabled, should total skill cap bonuses be awarded? (720 skills total at 4th level)
        public static bool SkillCapRewards { get; private set; }

        public static TimeSpan RewardInterval { get; private set; }

        // Each reward also takes this much time actually played, so an idle account earns nothing.
        public static TimeSpan PlayTimePerReward { get; private set; }

        public static RewardCategory[] Categories
        {
            get
            {
                if (m_Categories == null)
                {
                    SetupRewardTables();
                }

                return m_Categories;
            }
        }

        public static RewardList[] Lists
        {
            get
            {
                if (m_Lists == null)
                {
                    SetupRewardTables();
                }

                return m_Lists;
            }
        }

        public static bool HasAccess(Mobile mob, RewardCategory category)
        {
            var entries = category.Entries;

            for (var j = 0; j < entries.Count; ++j)
                // RewardEntry entry = entries[j];
            {
                if (HasAccess(mob, entries[j]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasAccess(Mobile mob, RewardEntry entry) =>
            Core.Expansion >= entry.RequiredExpansion && HasAccess(mob, entry.List, out var _);

        public static bool HasAccess(Mobile mob, RewardList list, out TimeSpan ts)
        {
            if (list == null)
            {
                ts = TimeSpan.Zero;
                return false;
            }

            if (mob.Account is not Account acct)
            {
                ts = TimeSpan.Zero;
                return false;
            }

            ts = list.Age - acct.AccountAge;

            return GetRewardLevel(acct) >= list.Level;
        }

        public static int GetRewardLevel(Mobile mob)
        {
            if (mob.Account is not Account acct)
            {
                return 0;
            }

            return GetRewardLevel(acct);
        }

        /// <summary>
        /// One level per <see cref="RewardInterval"/> of account age, but only as many as the account
        /// has played <see cref="PlayTimePerReward"/> for.
        /// </summary>
        public static int GetRewardLevel(Account acct) => GetRewardLevel(acct.AccountAge, acct.TotalGameTime);

        public static int GetRewardLevel(TimeSpan accountAge, TimeSpan played)
        {
            var byAge = (int)(accountAge.TotalDays / RewardInterval.TotalDays);
            var byPlay = PlayTimePerReward > TimeSpan.Zero ? (int)(played.TotalHours / PlayTimePerReward.TotalHours) : byAge;

            return Math.Max(Math.Min(byAge, byPlay), 0);
        }

        public static bool HasHalfLevel(Mobile mob)
        {
            if (mob.Account is not Account acct)
            {
                return false;
            }

            return HasHalfLevel(acct);
        }

        public static bool HasHalfLevel(Account acct) => acct.AccountAge.TotalDays / RewardInterval.TotalDays >= 0.5;

        public static bool ConsumeRewardPoint(Mobile mob)
        {
            ComputeRewardInfo(mob, out var cur, out var max);

            if (cur >= max)
            {
                return false;
            }

            if (mob.Account is not Account acct)
            {
                return false;
            }

            // if (mob.AccessLevel < AccessLevel.GameMaster)
            acct.SetTag("numRewardsChosen", (cur + 1).ToString());

            return true;
        }

        public static void ComputeRewardInfo(Mobile mob, out int cur, out int max)
        {
            ComputeRewardInfo(mob, out cur, out max, out _);
        }

        public static void ComputeRewardInfo(Mobile mob, out int cur, out int max, out int level)
        {
            if (mob.Account is not Account acct)
            {
                cur = max = level = 0;
                return;
            }

            level = GetRewardLevel(acct);

            if (level == 0)
            {
                cur = max = 0;
                return;
            }

            var tag = acct.GetTag("numRewardsChosen");

            if (string.IsNullOrEmpty(tag))
            {
                cur = 0;
            }
            else
            {
                cur = Utility.ToInt32(tag);
            }

            max = level;
        }

        public static bool CheckIsUsableBy(Mobile from, Item item, object[] args = null)
        {
            var isRelaxedRules = item is DyeTub or MonsterStatuette;

            var type = item.GetType();

            for (var i = 0; i < Lists.Length; ++i)
            {
                var list = Lists[i];
                var entries = list.Entries;

                for (var j = 0; j < entries.Length; ++j)
                {
                    if (entries[j].ItemType != type)
                    {
                        continue;
                    }

                    if (args == null && entries[j].Args.Length == 0)
                    {
                        if (isRelaxedRules && i <= 0 || HasAccess(from, list, out var ts))
                        {
                            return true;
                        }

                        // Your account is not old enough to use this item. Months until you can use this item :
                        from.SendLocalizedMessage(
                            1008126,
                            true,
                            Math.Ceiling(ts.TotalDays / 30.0).ToString()
                        );
                        return false;
                    }

                    if (args?.Length != entries[j].Args.Length)
                    {
                        continue;
                    }

                    var match = true;

                    for (var k = 0; match && k < args.Length; ++k)
                    {
                        match = args[k].Equals(entries[j].Args[k]);
                    }

                    if (match)
                    {
                        if (isRelaxedRules && i <= 0 || HasAccess(from, list, out var ts))
                        {
                            return true;
                        }

                        // Your account is not old enough to use this item. Months until you can use this item :
                        from.SendLocalizedMessage(
                            1008126,
                            true,
                            Math.Ceiling(ts.TotalDays / 30.0).ToString()
                        );
                        return false;
                    }
                }
            }

            // no entry?
            return true;
        }

        public static int GetRewardYearLabel(Item item, object[] args)
        {
            var level = GetRewardYear(item, args);

            return 1076216 + (level < 10 ? level :
                level < 12 ? level - 9 + 4240 : level - 11 + 37585);
        }

        public static int GetRewardYear(Item item, object[] args)
        {
            var type = item.GetType();

            for (var i = 0; i < Lists.Length; ++i)
            {
                var list = Lists[i];
                var entries = list.Entries;

                for (var j = 0; j < entries.Length; ++j)
                {
                    if (entries[j].ItemType == type)
                    {
                        if (args == null && entries[j].Args.Length == 0)
                        {
                            return i + 1;
                        }

                        if (args?.Length == entries[j].Args.Length)
                        {
                            var match = true;

                            for (var k = 0; match && k < args.Length; ++k)
                            {
                                match = args[k].Equals(entries[j].Args[k]);
                            }

                            if (match)
                            {
                                return i + 1;
                            }
                        }
                    }
                }
            }

            // no entry?
            return 0;
        }

        /// <summary>
        /// This shard offers only the monster statuettes and the special dye tubs, all of them from
        /// the first reward on. Statuettes from later expansions show only under those expansions.
        /// </summary>
        public static void SetupRewardTables()
        {
            var monsterStatues = new RewardCategory(1049750);
            var specialDyeTubs = new RewardCategory(1049753);

            m_Categories = new[] { monsterStatues, specialDyeTubs };

            m_Lists = new[]
            {
                new RewardList(
                    RewardInterval,
                    1,
                    new[]
                    {
                        new RewardEntry(specialDyeTubs, 1006008, typeof(RewardBlackDyeTub)),
                        new RewardEntry(specialDyeTubs, 1006013, typeof(FurnitureDyeTub)),
                        new RewardEntry(specialDyeTubs, 1006047, typeof(SpecialDyeTub)),
                        new RewardEntry(specialDyeTubs, 1006052, typeof(LeatherDyeTub)),
                        new RewardEntry(specialDyeTubs, 1049740, typeof(RunebookDyeTub)),
                        new RewardEntry(specialDyeTubs, 1049741, typeof(StatuetteDyeTub)),
                        Statuette(monsterStatues, 1006024, MonsterStatuetteType.Crocodile),
                        Statuette(monsterStatues, 1006025, MonsterStatuetteType.Daemon),
                        Statuette(monsterStatues, 1006026, MonsterStatuetteType.Dragon),
                        Statuette(monsterStatues, 1006027, MonsterStatuetteType.EarthElemental),
                        Statuette(monsterStatues, 1006028, MonsterStatuetteType.Ettin),
                        Statuette(monsterStatues, 1006029, MonsterStatuetteType.Gargoyle),
                        Statuette(monsterStatues, 1006030, MonsterStatuetteType.Gorilla),
                        Statuette(monsterStatues, 1006031, MonsterStatuetteType.Lich),
                        Statuette(monsterStatues, 1006032, MonsterStatuetteType.Lizardman),
                        Statuette(monsterStatues, 1006033, MonsterStatuetteType.Ogre),
                        Statuette(monsterStatues, 1006034, MonsterStatuetteType.Orc),
                        Statuette(monsterStatues, 1006035, MonsterStatuetteType.Ratman),
                        Statuette(monsterStatues, 1006036, MonsterStatuetteType.Skeleton),
                        Statuette(monsterStatues, 1006037, MonsterStatuetteType.Troll),
                        Statuette(monsterStatues, 1006038, MonsterStatuetteType.Cow),
                        Statuette(monsterStatues, 1006039, MonsterStatuetteType.Zombie),
                        Statuette(monsterStatues, 1006040, MonsterStatuetteType.Llama),
                        Statuette(monsterStatues, 1049742, MonsterStatuetteType.Ophidian),
                        Statuette(monsterStatues, 1049743, MonsterStatuetteType.Reaper),
                        Statuette(monsterStatues, 1049744, MonsterStatuetteType.Mongbat),
                        Statuette(monsterStatues, 1049768, MonsterStatuetteType.Gazer),
                        Statuette(monsterStatues, 1049769, MonsterStatuetteType.FireElemental),
                        Statuette(monsterStatues, 1049770, MonsterStatuetteType.Wolf),
                        new RewardEntry(
                            monsterStatues,
                            1080520,
                            typeof(MonsterStatuette),
                            Expansion.ML,
                            MonsterStatuetteType.Harrower
                        ),
                        new RewardEntry(
                            monsterStatues,
                            1080521,
                            typeof(MonsterStatuette),
                            Expansion.ML,
                            MonsterStatuetteType.Efreet
                        )
                    }
                )
            };
        }

        private static RewardEntry Statuette(RewardCategory category, int name, MonsterStatuetteType type) =>
            new(category, name, typeof(MonsterStatuette), type);

        public static void Configure()
        {
            Enabled = ServerConfiguration.GetOrUpdateSetting("vetRewards.enable", true);
            SkillCapRewards = ServerConfiguration.GetOrUpdateSetting("vetRewards.skillCapRewards", false);
            RewardInterval = ServerConfiguration.GetOrUpdateSetting("vetRewards.rewardInterval", TimeSpan.FromDays(365.0));
            PlayTimePerReward = ServerConfiguration.GetOrUpdateSetting(
                "vetRewards.playTimePerReward",
                TimeSpan.FromHours(100.0)
            );
        }

        [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
        public static void OnLogin(PlayerMobile pm)
        {
            if (!Enabled || !pm.Alive)
            {
                return;
            }

            ComputeRewardInfo(pm, out var cur, out var max, out var level);

            if (pm.SkillsCap is 7000 or 7050 or 7100 or 7150 or 7200)
            {
                level = Math.Clamp(level, 0, 4);

                if (SkillCapRewards)
                {
                    pm.SkillsCap = 7000 + level * 50;
                }
                else
                {
                    pm.SkillsCap = 7000;
                }
            }

            if (Core.ML && !pm.HasStatReward && HasHalfLevel(pm))
            {
                pm.HasStatReward = true;
                pm.StatCap += 5;
            }

            if (cur < max)
            {
                pm.SendGump(new RewardNoticeGump(pm));
            }
        }
    }

    public interface IRewardItem
    {
        bool IsRewardItem { get; set; }
    }
}
