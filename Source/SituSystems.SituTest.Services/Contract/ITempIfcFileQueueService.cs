using System.Threading.Tasks;

namespace SituSystems.SituTest.Services.Contract
{
    public interface ITempIfcFileQueueService
    {
        Task ProcessTempIfcFileAsync(TempIfcFileMessage tempIfcFileMessage);

        string ContainerName();
    }
}