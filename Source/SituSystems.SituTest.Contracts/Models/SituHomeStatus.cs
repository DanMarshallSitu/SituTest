using System;

namespace SituSystems.SituTest.Contracts.Models
{
    public class SituHomeStatus
    {
        public DateTime LastHealthyAt { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public string CurrentTask { get; set; }
    }
}
