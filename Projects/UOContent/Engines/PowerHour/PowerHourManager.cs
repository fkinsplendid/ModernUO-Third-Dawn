using System;
using Server.Mobiles;
using Server.Network;
using ModernUO.Serialization;

namespace Server.Engines.PowerHour
{
    [SerializationGenerator(0)]
    public partial class PowerHourManager
    {
        private static PowerHourManager _instance;
        private Timer _powerHourTimer;
        private Timer _scheduledTimer;

        [SerializableProperty(0)]
        public bool IsActive { get; private set; }

        [SerializableProperty(1)]
        public DateTime NextPowerHour { get; private set; }

        [SerializableProperty(2)]
        private TimeSpan Duration { get; set; }

        [SerializableProperty(3)]
        public double GainMultiplier { get; private set; }

        public static PowerHourManager Instance
        {
            get
            {
                _instance ??= new PowerHourManager();
                return _instance;
            }
        }

        private PowerHourManager()
        {
            IsActive = false;
            Duration = TimeSpan.FromHours(1);
            GainMultiplier = 2.0;
            NextPowerHour = DateTime.MinValue;
            Configure();
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            _instance = this;

            if (IsActive)
            {
                Timer.DelayCall(TimeSpan.Zero, EndPowerHour);
            }

            if (NextPowerHour < DateTime.UtcNow)
            {
                ScheduleNextPowerHour();
            }
            else
            {
                Timer.DelayCall(NextPowerHour - DateTime.UtcNow, StartPowerHour);
            }
        }

        public void Configure()
        {
            EventSink.WorldLoad += OnWorldLoad;
            EventSink.WorldSave += OnWorldSave;
        }

        private void OnWorldLoad()
        {
            if (IsActive)
            {
                Timer.DelayCall(TimeSpan.Zero, EndPowerHour);
            }
            else if (NextPowerHour == DateTime.MinValue || NextPowerHour < DateTime.Now)
            {
                // Only schedule if we don't have a valid future time
                ScheduleNextPowerHour();
            }
            else
            {
                // Restore the existing schedule
                _scheduledTimer = Timer.DelayCall(NextPowerHour - DateTime.Now, StartPowerHour);
            }
        }

        private void OnWorldSave()
        {
            // Not needed - serialization is handled automatically
        }

        public void ScheduleNextPowerHour()
        {
            _scheduledTimer?.Stop();

            var now = DateTime.Now;
            var today8PM = now.Date.AddHours(20); // 8 PM today local time

            // If it's already past 8 PM, schedule for tomorrow
            NextPowerHour = now >= today8PM ? today8PM.AddDays(1) : today8PM;

            var delay = NextPowerHour - now;
            if (delay.TotalMinutes < 0) // If we somehow got a negative delay
            {
                // Force schedule to tomorrow
                NextPowerHour = today8PM.AddDays(1);
                delay = NextPowerHour - now;
            }

            Console.WriteLine($"[PowerHour] Scheduling next power hour for {NextPowerHour:yyyy-MM-dd HH:mm:ss} local time (in {delay.TotalHours:F1} hours)");

            _scheduledTimer = Timer.DelayCall(delay, StartPowerHour);
        }

        public void StartPowerHour()
        {
            if (IsActive)
                return;

            IsActive = true;

            foreach (NetState ns in NetState.Instances)
            {
                if (ns.Mobile is PlayerMobile player)
                {
                    player.SendMessage(0x35, "Power Hour has begun! All skill and stat gains are doubled for the next hour!");
                }
            }

            _powerHourTimer?.Stop();
            _powerHourTimer = Timer.DelayCall(Duration, EndPowerHour);
        }

        public void EndPowerHour()
        {
            if (!IsActive)
                return;

            _powerHourTimer?.Stop();
            IsActive = false;

            foreach (NetState ns in NetState.Instances)
            {
                if (ns.Mobile is PlayerMobile player)
                {
                    player.SendMessage(0x35, "Power Hour has ended. Normal gain rates have been restored.");
                }
            }

            ScheduleNextPowerHour();
        }

        public bool CheckGain(Mobile mobile)
        {
            if (!IsActive || mobile == null || !mobile.Player)
                return false;

            return true;
        }
    }
}
