using System;
using Serilog;
using SituSystems.SituTest.Services.ServiceCheckers;

namespace SituSystems.SituTest.Services.Contract
{
    public class Error
    {
        public Error(string message, Exception exception, Uri uri, ServiceChecker serviceChecker)
        {
            ServiceChecker = serviceChecker;
            Message = message;
            Exception = exception;
            Uri = uri;
        }

        public string Message { get; }
        public ServiceChecker ServiceChecker { get; }
        public Exception Exception { get; }
        public Uri Uri { get; }

        public void Log()
        {
            if (Exception != null)
            {
                Serilog.Log.Error(Exception, "Error in {ServiceName}: {Error}", ServiceChecker.Name, Message);
            }
            else
            {
                Serilog.Log.Error("Error in {ServiceName}: {Error}", ServiceChecker.Name, Message);
            }
        }
    }
}