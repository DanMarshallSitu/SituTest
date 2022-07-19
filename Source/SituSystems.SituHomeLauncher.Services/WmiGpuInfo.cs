using SituSystems.SituHomeLauncher.Services.Contract;
using System.Collections.Generic;
using System.Management;

namespace SituSystems.SituHomeLauncher.Services
{
#pragma warning disable CA1416 // Validate platform compatibility
    public class WmiGpuInfo : IGpuInfo
    {
        public List<(VideoController VideoController, string ErrorMessage)> GetInfo()
        {
            var result = new List<(VideoController VideoController, string ErrorMessage)>();

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
            {
                var managmentObjects = searcher.Get();
                foreach (ManagementObject obj in managmentObjects)
                {
                    (VideoController VideoController, string ErrorMessage) vid;
                    try
                    {
                        vid.VideoController = new VideoController
                        {
                            Name = obj["Name"].ToString(),                            
                        };
                        vid.ErrorMessage = string.Empty;
                    }
                    catch
                    {
                        vid.VideoController = null; 
                        vid.ErrorMessage = ("Unable to obtain GPU information");
                    }

                    result.Add(vid);
                };
            }

            return result;
        }
    }
#pragma warning restore CA1416 // Validate platform compatibility
}
