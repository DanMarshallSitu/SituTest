using System.Threading.Tasks;

namespace SituSystems.SituTest.Services.Contract
{
    public interface IArtifactStoreService
    {
        Task CheckForNextRecipeToCook();
    }
}