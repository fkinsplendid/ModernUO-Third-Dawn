using System;

namespace Server.Engines.VeteranRewards
{
    public class RewardList
    {
        public RewardList(TimeSpan interval, int index, RewardEntry[] entries)
        {
            Level = index;
            Age = TimeSpan.FromDays(interval.TotalDays * index);
            Entries = entries;

            for (var i = 0; i < entries.Length; ++i)
            {
                entries[i].List = this;
            }
        }

        /// <summary>The reward level an account needs to choose from this list.</summary>
        public int Level { get; }

        public TimeSpan Age { get; }

        public RewardEntry[] Entries { get; }
    }
}
