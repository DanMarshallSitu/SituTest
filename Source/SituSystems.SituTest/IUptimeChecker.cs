using System.Threading.Tasks;

namespace SituSystems.SituTest.Services
{
    public interface IUptimeChecker
    {
        public Task Run();
    }
}