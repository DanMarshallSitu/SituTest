using System.Threading.Tasks;

namespace SituSystems.SituHomeLauncher.Services.Contract
{
    public interface ITempIfcFileQueueService
    {
        Task ProcessTempIfcFileAsync(TempIfcFileMessage tempIfcFileMessage);

        string ContainerName();
    }
}