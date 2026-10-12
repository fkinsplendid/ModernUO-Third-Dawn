namespace Server.Engines.PowerHour
{
    public static class PowerHourSystem
    {
        public static void Initialize()
        {
            PowerHourManager.Instance.Configure();
        }
    }
}
