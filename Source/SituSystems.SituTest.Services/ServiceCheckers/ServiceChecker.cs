using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SituSystems.SituTest.Services.Contract;

namespace SituSystems.SituTest.Services.ServiceCheckers
{
    public abstract class ServiceChecker
    {
        public abstract string Name { get; }
        public abstract bool IsScreenShotRequired { get; }
        public abstract int MaxRetryAttempts { get; }
        private byte[] Screenshot { get; set; }
        public List<Error> Errors { get; private set; }
        public Uri Uri { get; protected set; }
        protected abstract bool RunCheckInternal();

        public bool RunCheckAndGetResult()
        {
            Errors = new();

            return RunCheckInternal();
        }

        public void AddError(string message, Exception exception = null)
        {
            Errors.Add(new Error(message: message, exception: exception, uri: Uri, serviceChecker: this));
        }

        protected static ChromeDriver GetChromeDriver()
        {
            var options = new ChromeOptions();
            options.AddExcludedArguments(new List<string> { "excludeSwitches", "enable-logging" });
            var driver = new ChromeDriver(options);
            return driver;
        }

        protected void CaptureFullScreenshot(ITakesScreenshot driver)
        {
            try
            {
                var screenshot = driver.GetScreenshot();
                Screenshot = screenshot.AsByteArray;
            }
            catch (Exception e)
            {
                Screenshot = null;
                AddError("Unable to take full screen screenshot", e);
            }
        }

        public void LogErrors()
        {
            foreach (var error in Errors)
            {
                error.Log();
            }
        }
    }
}