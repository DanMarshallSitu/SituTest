using System;
using System.Drawing;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SituSystems.SituTest.Services.ServiceCheckers;

namespace SituSystems.SituTest.Services
{
    public class PanoramaCheckerFactory
    {
        private readonly PanoramaCheckerSettings _panoramaCheckerSettings;

        public PanoramaCheckerFactory(AppSettings settings)
        {
            _panoramaCheckerSettings = settings.PanoramaCheckers;
        }

        public ServiceChecker SituDemoPanoramaChecker
        {
            get
            {
                var situ = _panoramaCheckerSettings.Situ;
                return new PanoramaChecker("Situ Demo",
                    situ.PanoramaUrl,
                    driver =>
                    {
                        driver.SituLogin(situ.LoginUrl, situ.UserName, situ.Password);
                        driver.Manage().Window.Size = new Size(800, 1800);
                        driver.Navigate().GoToUrl(situ.PanoramaUrl);
                        var panoElement = driver.GetElementWithWait(By.CssSelector("canvas"));
                        Thread.Sleep(TimeSpan.FromSeconds(situ.PanoramaLoadDelayInSeconds));
                        driver.ScrollTo(panoElement);
                        return panoElement;
                    },
                    situ.MaxRetryAttempts);
            }
        }

        public ServiceChecker BurbankPanoramaChecker
        {
            get
            {
                var burbank = _panoramaCheckerSettings.Burbank;
                return new PanoramaChecker("Burbank",
                    burbank.PanoramaUrl,
                    driver =>
                    {
                        driver.Navigate().GoToUrl(burbank.PanoramaUrl);
                        driver.Manage().Window.Size = new Size(800, 1800);
                        driver.FindElement(By.Id("myplace-tab")).Click();
                        var panoElement = driver.GetElementWithWait(By.CssSelector("#pano-pano"));
                        Thread.Sleep(TimeSpan.FromSeconds(burbank.PanoramaLoadDelayInSeconds));
                        panoElement.Click();
                        return panoElement;
                    },
                    burbank.MaxRetryAttempts);
            }
        }
    }
}