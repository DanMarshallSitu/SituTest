namespace SituSystems.SituTest.Contracts.Models
{
    public class RenderStatus
    {
        public RenderStatusEnum Status { get; set; }
        public int FilesExpected { get; set; }
        public string Errors { get; set; }
    }
}
