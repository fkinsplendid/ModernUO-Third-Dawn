namespace Server.Engines.PowerHour
{
    public static class PowerHourExtensions
    {
        public static double GetGainModifier(this Mobile mobile)
        {
            if (PowerHourManager.Instance != null && PowerHourManager.Instance.CheckGain(mobile))
                return PowerHourManager.Instance.GainMultiplier;

            return 1.0;
        }
    }
}
