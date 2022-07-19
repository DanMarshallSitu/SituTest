namespace SituSystems.SituHomeLauncher.Services.AzureServiceBusApi
{
    public class ServiceBusSettings : Core.AzureServiceBus.ServiceBusSettings
    {
        public string Url { get; set; }
        public string Key { get; set; }
        public ServiceBusTopics Topics { get; set; }
        public ServiceBusQueues Queues { get; set; }
    }

    public class ServiceBusTopics
    {
        public string RenderEvent { get; set; }
        public string ArtifactFileAdded { get; set; }
    }
    public class ServiceBusQueues
    {
        public string TempIfcFiles { get; set; }
    }
}
