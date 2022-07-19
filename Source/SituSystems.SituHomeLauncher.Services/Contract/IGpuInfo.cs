using System.Collections.Generic;

namespace SituSystems.SituHomeLauncher.Services.Contract
{
    public interface IGpuInfo
    {
        public List<(VideoController VideoController, string ErrorMessage)> GetInfo();
    }
}