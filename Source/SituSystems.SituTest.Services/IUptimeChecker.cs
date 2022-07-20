using System.Collections.Generic;
using System.Threading.Tasks;
using SituSystems.SituTest.Services.ServiceCheckers;

namespace SituSystems.SituTest.Services
{
    public interface IUptimeChecker
    {
        public Task Run(List<ServiceChecker> serviceCheckers);
    }
}