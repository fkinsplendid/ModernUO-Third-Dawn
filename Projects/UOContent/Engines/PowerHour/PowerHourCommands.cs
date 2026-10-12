using System;
using Server.Engines.PowerHour;

namespace Server.Commands
{
    public static class PowerHourCommands
    {
        public static void Initialize()
        {
            // Existing GM commands
            CommandSystem.Register("StartPowerHour", AccessLevel.GameMaster, StartPowerHour_OnCommand);
            CommandSystem.Register("EndPowerHour", AccessLevel.GameMaster, EndPowerHour_OnCommand);

            // New player command
            CommandSystem.Register("PowerHour", AccessLevel.Player, PowerHour_OnCommand);
        }

        [Usage("StartPowerHour")]
        [Description("Manually starts a Power Hour event.")]
        private static void StartPowerHour_OnCommand(CommandEventArgs e)
        {
            PowerHourManager.Instance.StartPowerHour();
            e.Mobile.SendMessage("Power Hour has been started manually.");
        }

        [Usage("EndPowerHour")]
        [Description("Manually ends the current Power Hour event.")]
        private static void EndPowerHour_OnCommand(CommandEventArgs e)
        {
            PowerHourManager.Instance.EndPowerHour();
            e.Mobile.SendMessage("Power Hour has been ended manually.");
        }

        // Existing GM command handlers...

        [Usage("PowerHour")]
        [Description("Shows the time remaining until the next Power Hour event.")]
        private static void PowerHour_OnCommand(CommandEventArgs e)
        {
            var nextPowerHour = PowerHourManager.Instance.NextPowerHour;
            var timeUntil = nextPowerHour - DateTime.Now;

            if (PowerHourManager.Instance.IsActive)
            {
                e.Mobile.SendMessage(0x35, "Power Hour is currently active!");
            }
            else if (timeUntil.TotalMinutes <= 0)
            {
                // Force a reschedule if we have negative time
                PowerHourManager.Instance.ScheduleNextPowerHour();
                timeUntil = PowerHourManager.Instance.NextPowerHour - DateTime.Now;

                var hours = (int)timeUntil.TotalHours;
                var minutes = (int)timeUntil.Minutes;

                if (hours > 0)
                {
                    e.Mobile.SendMessage(0x35, $"Next Power Hour begins in {hours} hour{(hours != 1 ? "s" : "")} and {minutes} minute{(minutes != 1 ? "s" : "")}.");
                }
                else
                {
                    e.Mobile.SendMessage(0x35, $"Next Power Hour begins in {minutes} minute{(minutes != 1 ? "s" : "")}.");
                }
            }
            else
            {
                var hours = (int)timeUntil.TotalHours;
                var minutes = (int)timeUntil.Minutes;

                if (hours > 0)
                {
                    e.Mobile.SendMessage(0x35, $"Next Power Hour begins in {hours} hour{(hours != 1 ? "s" : "")} and {minutes} minute{(minutes != 1 ? "s" : "")}.");
                }
                else
                {
                    e.Mobile.SendMessage(0x35, $"Next Power Hour begins in {minutes} minute{(minutes != 1 ? "s" : "")}.");
                }
            }
        }
    }
}
