namespace SituSystems.SituTest.Services
{
    public class AppSettings
    {
        public string InstanceName { get; set; }
        public int CheckPeriodInSeconds { get; set; }
        public int UnhandledExceptionWaitInSeconds { get; set; }
        public PanoramaCheckerSettings PanoramaCheckers { get; set; }
        public FeatureFlags FeatureFlags { get; set; }
    }
}