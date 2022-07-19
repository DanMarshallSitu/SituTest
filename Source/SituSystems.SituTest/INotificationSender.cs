namespace SituSystems.SituTest.Services
{
    public interface INotificationSender
    {
        void SendError(IServiceChecker checker);
    }
}