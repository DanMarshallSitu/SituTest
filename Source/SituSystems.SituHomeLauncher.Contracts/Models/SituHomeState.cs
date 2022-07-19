namespace SituSystems.SituHomeLauncher.Contracts.Models
{
    public class SituHomeState
    {
        public SituHomeStatus Status { get; set; }
        public RenderStatusEnum RenderStatus { get; set; }
        public string CurrentLauncherTaskId { get; set; }
        public RenderTask CurrentRenderTask { get; set; }
        public string RenderError { get; set; }
    }
}
