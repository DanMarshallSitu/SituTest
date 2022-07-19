namespace SituSystems.SituTest.Contracts.Models
{
    public class SituTestRenderTask : SituTestTask
    {
        public string SituTestTaskId { get; set; }
        public RenderTask RenderTask { get; set; }
        public bool IncludeDepthMaps { get; set; }
        public bool IncludeNavigationPoints { get; set; }
    }
}
