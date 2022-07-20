namespace SituSystems.SituTest.Services
{
    public class PanoramaSettings
    {
        public int PanoramaLoadDelayInSeconds { get; set; }
        public int PanoramaRetryDelayInSeconds { get; set; }
        public string PanoramaUrl { get; set; }
        public string LoginUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int MaxRetryAttempts { get; set; }
    }
}