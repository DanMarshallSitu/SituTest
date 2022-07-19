using System.Collections.Generic;

namespace SituSystems.SituHomeLauncher.Contracts.Models
{
    public class HomeConfig
    {
        public string Builder { get; set; }
        public string Home { get; set; }
        public string MasterFile { get; set; }
        public List<string> Options { get; set; }
        public List<string> Themes { get; set; }
    }
}