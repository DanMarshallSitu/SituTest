using System;

namespace SituSystems.SituHomeLauncher.Contracts.Models
{
    public class RenderTask
    {
        public HomeConfig HomeConfig { get; set; }
        public Guid HomeConfigId { get; set; }
        public string StartingRoom { get; set; }
    }
}
