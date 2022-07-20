namespace SituSystems.SituTest.Services
{
    public class PanoramaCheckerSettings
    {
        public int CheckPeriodInMinutes { get; set; }
        public AzureStorageCredentials AzureStorageCredentials { get; set; }
        public PanoramaSettings Situ { get; set; }
        public PanoramaSettings Burbank { get; set; }
    }
}