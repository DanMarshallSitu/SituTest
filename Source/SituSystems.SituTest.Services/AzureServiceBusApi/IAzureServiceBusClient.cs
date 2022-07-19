using SituSystems.SituTest.Contracts.Models;
using System.Threading.Tasks;

namespace SituSystems.SituTest.Services.AzureServiceBusApi
{
    public interface IAzureServiceBusClient
    {
        Task<RenderTask> GetRenderTask(string topic, string subscription);
        Task<string> GetTask(string topic, string subscription);
        Task<(T responseObject, string correlationId)> GetMessage<T>(string queue);
    }
}
