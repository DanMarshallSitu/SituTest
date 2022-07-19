using System.Threading.Tasks;

namespace SituSystems.SituHomeLauncher.Services.Contract
{
    public interface IArtifactStoreService
    {
        Task CheckForNextRecipeToCook();
    }
}