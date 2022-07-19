namespace SituSystems.SituTest.Services
{
    public interface IServiceChecker
    {
        public string ServiceName { get; }

        public bool IsScreenShotRequired { get; }
        public byte[] Screenshot { get; }
        public bool RunContentCheck();
        public string GetErrorWarning();
    }
}