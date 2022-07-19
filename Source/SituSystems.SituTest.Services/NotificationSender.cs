using Serilog;

namespace SituSystems.SituTest.Services
{
    public class NotificationSender : INotificationSender
    {
        public void SendError(IServiceChecker checker)
        {
            Log.Error("Error in {ServiceName}: {Message}",
                checker.ServiceName,
                checker.GetErrorWarning()
                //checker.Screenshot
            );
        }
    }
}