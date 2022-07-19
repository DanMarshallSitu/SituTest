using System.Collections.Generic;
using SituSystems.Core.AzureServiceBus;

namespace SituSystems.SituTest.Services
{
    public class PanoramaCheckerSettings
    {
        public AzureStorageCredentials AzureStorageCredentials { get; set; }
        public string BurbankPanoramaUrl { get; set; }
        public int CheckPeriodInMinutes { get; set; }
        public int PanoramaLoadDelayInSeconds { get; set; }
        public int PanoramaRetryDelayInSeconds { get; set; }
        public string SituDemoUrl { get; set; }
        public string SituLoginUrl { get; set; }
        public string SituPortalPass { get; set; }
        public string SituPortalUser { get; set; }
    }

    public class AzureStorageCredentials { }
}