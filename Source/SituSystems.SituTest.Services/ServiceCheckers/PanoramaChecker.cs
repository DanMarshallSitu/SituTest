using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SituSystems.SituTest.Services.ServiceCheckers
{
    public class PanoramaChecker : IServiceChecker
    {
        private readonly Func<ChromeDriver, IWebElement> _getPanoramaElement;
        private readonly int _retryDelayInSeconds;
        private readonly string _url;
        private string _error;

        public PanoramaChecker(string siteName,
            string url,
            Func<ChromeDriver, IWebElement> getPanoramaElement,
            int retryDelayInSeconds)
        {
            ServiceName = siteName;
            _url = url;
            _getPanoramaElement = getPanoramaElement;
            _retryDelayInSeconds = retryDelayInSeconds;
        }

        public bool RunContentCheck()
        {
            var driver = GetDriver();

            driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(4);
            var isAllBlack = true;

            try
            {
                var element = _getPanoramaElement(driver);

                for (var retryAttempt = 1; retryAttempt <= 3; retryAttempt++)
                {
                    using var image = driver.GetScreenshotStream(element);
                    {
                        CropPanoramaToBottomRightQuadrant(image, element);
                        isAllBlack = CheckImageForAllBlackPixels(image);

                        if (isAllBlack)
                        {
                            Log.Information("Black panorama encountered, retrying. Attempt " + retryAttempt, _url);
                            Thread.Sleep(TimeSpan.FromSeconds(_retryDelayInSeconds));
                        }

                        else
                        {
                            break;
                        }
                    }
                }

                CaptureFullScreenshot(driver);
                if (isAllBlack)
                {
                    _error = $"Black panorama encountered. Please investigate. URL of failed panorama is {_url}";

                    return false;
                }
                else
                {
                    Log.Information($"Panorama check successful for {ServiceName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                CaptureFullScreenshot(driver);
                Log.Error(ex, $"An error has occurred in {nameof(PanoramaChecker)}.");
                _error = ex.Message;
                return false;
            }
            finally
            {
                driver.Quit();
            }
        }

        public string GetErrorWarning()
        {
            return $"Error checking {ServiceName} panorama. {_error}";
        }

        public string ServiceName { get; }

        public byte[] Screenshot { get; private set; }

        public bool IsScreenShotRequired => true;

        private string PersistScreenShotToBlobStorage(ChromeDriver driver)
        {
            var urlToBlob = "";
            return urlToBlob;
        }

        private bool CheckImageForAllBlackPixels(Image<Rgba32> image)
        {
            var isAllBlack = true;

            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (var x = 0; x < pixelRow.Length; x++)
                    {
                        ref var pixel = ref pixelRow[x];

                        if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
                        {
                            isAllBlack = false;
                            return;
                        }
                    }
                }
            });

            return isAllBlack;
        }

        private static ChromeDriver GetDriver()
        {
            var options = new ChromeOptions();
            options.AddExcludedArguments(new List<string> {"excludeSwitches", "enable-logging"});
            var driver = new ChromeDriver(options);
            return driver;
        }

        private static void CropPanoramaToBottomRightQuadrant(Image image, IWebElement element)
        {
            var x = element.Location.X;
            var y = element.Location.Y;
            var w = element.Size.Width;
            var h = element.Size.Height;

            // cropped rectangle dimensions  
            var h2 = h / 2;
            var w2 = w / 2;
            var x2 = x;
            var y2 = y + h2 - 1;

            var time = DateTime.Now;

            //Todo: remove test screenshots
            image.SaveAsPng($@"c:\temp\{time:hh mm ss}.png");

            image.Mutate(c => { c.Crop(new(x2, y2, w2, h2)); });

            image.SaveAsPng($@"c:\temp\{time:hh mm ss} - Cropped.png");
        }

        private void CaptureFullScreenshot(ITakesScreenshot driver)
        {
            try
            {
                var screenshot = driver.GetScreenshot();
                Screenshot = screenshot.AsByteArray;
            }
            catch (Exception e)
            {
                Log.Error(e, "Unable to capture full screen screenshot!");
                throw;
            }
        }
    }
}