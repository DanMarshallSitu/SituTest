using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Serilog;
using SituSystems.SituTest.Services.ServiceCheckers;

namespace SituSystems.SituTest.Services
{
    public class UptimeChecker : IUptimeChecker
    {
        private readonly INotificationSender _notificationSender;
        private readonly List<IServiceChecker> _serviceCheckers;
        private readonly PanoramaCheckerSettings _settings;

        public UptimeChecker(INotificationSender notificationSender,
            IOptions<PanoramaCheckerSettings> appSettings)
        {
            _notificationSender = notificationSender;
            _settings = appSettings.Value;

            var situDemoPanoChecker = new PanoramaChecker("Situ Demo",
                _settings.SituDemoUrl,
                GetSituDemoPano,
                _settings.PanoramaRetryDelayInSeconds);

            var burbankPanoChecker = new PanoramaChecker("Burbank",
                _settings.BurbankPanoramaUrl,
                GetBurbankPanoElement,
                _settings.PanoramaRetryDelayInSeconds);

            _serviceCheckers = new() {burbankPanoChecker, situDemoPanoChecker};
        }

        public async Task Run()
        {
            var checkSuccessful = true;

            if (!Directory.Exists(Path.GetTempPath() + "/1")) Directory.CreateDirectory(Path.GetTempPath() + "/1");

            // Cycle through all registered checkers
            foreach (var checker in _serviceCheckers)
            {
                try
                {
                    if (!checker.RunContentCheck())
                    {
                        checkSuccessful = false;
                        _notificationSender.SendError(checker);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception encountered in running check");
                    checkSuccessful = false;
                    _notificationSender.SendError(checker);
                }
            }

            Console.WriteLine($"UptimeCheck successful: {checkSuccessful}");

            Thread.Sleep(TimeSpan.FromMinutes(_settings.CheckPeriodInMinutes));
        }

        private IWebElement GetBurbankPanoElement(ChromeDriver driver)
        {
            driver.Navigate().GoToUrl(_settings.BurbankPanoramaUrl);
            driver.Manage().Window.Size = new Size(800, 1800);
            driver.FindElement(By.Id("myplace-tab")).Click();
            var panoElement = driver.GetElementWithWait(By.CssSelector("#pano-pano"));
            Thread.Sleep(TimeSpan.FromSeconds(_settings.PanoramaLoadDelayInSeconds));
            panoElement.Click();
            return panoElement;
        }

        private IWebElement GetSituDemoPano(ChromeDriver driver)
        {
            driver.SituLogin(_settings.SituLoginUrl, _settings.SituPortalUser, _settings.SituPortalPass);
            driver.Manage().Window.Size = new Size(800, 1800);
            driver.Navigate().GoToUrl(_settings.SituDemoUrl);
            var panoElement = driver.GetElementWithWait(By.CssSelector("canvas"));
            Thread.Sleep(TimeSpan.FromSeconds(_settings.PanoramaLoadDelayInSeconds));
            driver.ScrollTo(panoElement);
            return panoElement;
        }
    }
}