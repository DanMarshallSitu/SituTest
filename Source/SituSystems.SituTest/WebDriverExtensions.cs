using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Size = System.Drawing.Size;

namespace SituSystems.SituTest.Services
{
    public static class WebDriverExtensions
    {
        public static Image<Rgba32> GetScreenshotStream(this IWebDriver driver, IWebElement element)
        {
            var actions = new Actions(driver);
            actions.MoveToElement(element).Perform();
            var byteArray = ((ITakesScreenshot) driver).GetScreenshot().AsByteArray;
            var image = Image.Load<Rgba32>(byteArray);

            return image;
        }

        public static void SituLogin(this IWebDriver driver, string loginUrl, string userName, string password)
        {
            driver.Navigate().GoToUrl(loginUrl);
            Thread.Sleep(TimeSpan.FromSeconds(1));
            driver.Manage().Window.Size = new Size(800, 800);
            driver.FindElement(By.Id("Email")).Click();
            driver.FindElement(By.Id("Email")).SendKeys(userName);
            driver.FindElement(By.Id("Password")).Click();
            driver.FindElement(By.Id("Password")).SendKeys(password);
            driver.FindElement(By.Name("button")).Click();
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }


        public static void ScrollTo(this ChromeDriver driver, IWebElement element)
        {
            var actions = new Actions(driver);
            actions.MoveToElement(element).Perform();
        }


        public static IWebElement GetElementWithWait(this IWebDriver driver, By selector)
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
            return wait.Until(ExpectedConditions.ElementIsVisible(selector));
        }
    }
}