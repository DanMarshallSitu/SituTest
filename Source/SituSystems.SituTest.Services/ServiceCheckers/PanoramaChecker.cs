using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Serilog;
using SituSystems.SituTest.Services.Contract;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SituSystems.SituTest.Services.ServiceCheckers
{
    public class PanoramaChecker : ServiceChecker
    {
        private readonly Func<ChromeDriver, IWebElement> _getPanoramaElement;

        public PanoramaChecker(string siteName, string uri, Func<ChromeDriver, IWebElement> getPanoramaElement, int maxRetryAttempts = 1)
        {
            Name = $"{siteName} panorama checker";
            Uri = new Uri(uri);
            _getPanoramaElement = getPanoramaElement;
            MaxRetryAttempts = maxRetryAttempts;
        }

        public override string Name { get; }
        public override bool IsScreenShotRequired => true;
        public override int MaxRetryAttempts { get; }
        protected override bool RunCheckInternal()
        {
            using (var driver = GetChromeDriver())
            {
                try
                {
                    driver.Manage().Timeouts().PageLoad = TimeSpan.FromMinutes(4);
                    var element = _getPanoramaElement(driver);
                    CaptureFullScreenshot(driver);
                    using var image = driver.GetScreenshotStream(element);
                    var isBlack = IsBottomRightQuadrantBlack(image, element);
                    if (isBlack)
                    {
                        AddError("Black panorama encountered. Please investigate.");

                        return false;
                    }
                }
                catch
                {
                    CaptureFullScreenshot(driver);
                    throw;
                }
                finally
                {
                    driver.Quit();
                }
            }

            Log.Information($"Panorama check successful for {Name}");
            return true;
        }

        private bool IsBottomRightQuadrantBlack(Image<Rgba32> image, IWebElement element)
        {
            CropPanoramaToBottomRightQuadrant(image, element);
            return CheckImageForAllBlackPixels(image);
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
    }
}