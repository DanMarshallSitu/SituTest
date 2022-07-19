namespace SituSystems.SituHomeLauncher.Contracts.Models
{
    public class LauncherRenderTask : LauncherTask
    {
        public string LauncherTaskId { get; set; }
        public RenderTask RenderTask { get; set; }
        public bool IncludeDepthMaps { get; set; }
        public bool IncludeNavigationPoints { get; set; }
    }
}
