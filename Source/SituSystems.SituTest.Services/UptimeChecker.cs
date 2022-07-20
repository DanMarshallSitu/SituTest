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
        private readonly List<ServiceCheckerBase> _serviceCheckers;
        private readonly PanoramaCheckerSettings _settings;

        public UptimeChecker(IOptions<AppSettings> appSettings)
        {
            _settings = appSettings.Value.PanoramaCheckers;

            var situDemoPanoChecker = new PanoramaChecker("Situ Demo",
                _settings.Situ.PanoramaUrl,
                GetSituDemoPano, 
                _settings.Situ.MaxRetryAttempts);

            var burbankPanoChecker = new PanoramaChecker("Burbank",
                _settings.Burbank.PanoramaUrl,
                GetBurbankPanoElement,
                _settings.Burbank.MaxRetryAttempts);

            _serviceCheckers = new() {burbankPanoChecker, situDemoPanoChecker};
        }

        public async Task Run()
        {
            // Cycle through all registered checkers
            foreach (var checker in _serviceCheckers)
            {
                var checkSuccessful = false;
                try
                {
                    var currentAttempt = 1;
                    bool ExceededMaxAttempts() => currentAttempt > checker.MaxRetryAttempts;
                    while (!ExceededMaxAttempts() && !checkSuccessful)
                    {
                        var messageTemplate = $"Running {{CheckerName}}, attempt {currentAttempt} of {checker.MaxRetryAttempts}";
                        Log.Information(messageTemplate, checker.Name);
                        checkSuccessful = checker.RunCheckAndGetResult();
                        if (!checkSuccessful)
                        {
                            Log.Information("{CheckerName} check failed", checker.Name);
                        }

                        currentAttempt++;
                    }

                    if (ExceededMaxAttempts())
                    {
                        checker.LogErrors();
                    }
                }
                catch (Exception ex)
                {
                    checker.AddError("Exception encountered while running check", ex);
                    checker.LogErrors();
                }
            }
        }

        private IWebElement GetBurbankPanoElement(ChromeDriver driver)
        {
            var burbank = _settings.Burbank;
            driver.Navigate().GoToUrl(burbank.PanoramaUrl);
            driver.Manage().Window.Size = new Size(800, 1800);
            driver.FindElement(By.Id("myplace-tab")).Click();
            var panoElement = driver.GetElementWithWait(By.CssSelector("#pano-pano"));
            Thread.Sleep(TimeSpan.FromSeconds(burbank.PanoramaLoadDelayInSeconds));
            panoElement.Click();
            return panoElement;
        }

        private IWebElement GetSituDemoPano(ChromeDriver driver)
        {
            var situ = _settings.Situ;
            driver.SituLogin(situ.LoginUrl, situ.UserName, situ.Password);
            driver.Manage().Window.Size = new Size(800, 1800);
            driver.Navigate().GoToUrl(situ.PanoramaUrl);
            var panoElement = driver.GetElementWithWait(By.CssSelector("canvas"));
            Thread.Sleep(TimeSpan.FromSeconds(situ.PanoramaLoadDelayInSeconds));
            driver.ScrollTo(panoElement);
            return panoElement;
        }
    }
}