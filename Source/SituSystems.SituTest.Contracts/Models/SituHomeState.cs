namespace SituSystems.SituTest.Contracts.Models
{
    public class SituHomeState
    {
        public SituHomeStatus Status { get; set; }
        public RenderStatusEnum RenderStatus { get; set; }
        public string CurrentSituTestTaskId { get; set; }
        public RenderTask CurrentRenderTask { get; set; }
        public string RenderError { get; set; }
    }
}