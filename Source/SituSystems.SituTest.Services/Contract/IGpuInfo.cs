using System.Collections.Generic;

namespace SituSystems.SituTest.Services.Contract
{
    public interface IGpuInfo
    {
        public List<(VideoController VideoController, string ErrorMessage)> GetInfo();
    }
}