using System;
using Serilog;
using SituAnalytics.Contracts.Models.Enums;
using SituAnalytics.Contracts.Queries;
using SituAnalytics.WebApi.Client.Contracts;

namespace SituSystems.SituTest.Services.ServiceCheckers
{
    public class ProductReportChecker : IServiceChecker
    {
        private readonly ISituAnalyticsWebApiClient _client;
        private string _error;

        public ProductReportChecker(ISituAnalyticsWebApiClient client)
        {
            _client = client;
        }

        public bool RunContentCheck()
        {
            _error = "";

            var clockOffTime = new TimeSpan(22, 0, 0);
            var startUpTime = new TimeSpan(8, 0, 0);
            var currentTime = DateTime.Now.TimeOfDay;

            // On average, the products will get maybe 0-5 items between 10pm and 5am. Because of this, we'll just turn off during this time.
            if (clockOffTime < currentTime || currentTime < startUpTime)
                // Won't classify this as errored.
                return true;

            try
            {
                // Check over a 3 hour interval.
                var from = DateTime.Now.AddHours(-3);
                var to = DateTime.Now;

                var report = _client.ListProductImpressionsAsync(new ListProductImpressionsRequest
                {
                    PageSize = 25,
                    DateFrom = from,
                    DateTo = to,
                    PageNumber = 1,
                    ReportType = ProductImpressionReportType.PerProduct
                });
                report.Wait();

                if (!report.IsCompletedSuccessfully)
                {
                    _error = "Could not check for the Product Impressions list. Could this be down?";
                    return false;
                }

                var count = 0;
                if (report.IsCompletedSuccessfully)
                {
                    if (report.Result.Items != null)
                        foreach (var item in report.Result.Items)
                            count += item.TotalImpressions;
                    else _error = "The report for Product Impressions returned no data.";
                }

                // Checking over an interval of 3 hours, as prime time would usually present anything above 50, anything less than
                // 10 may be a problem.
                if (count <= 10)
                {
                    _error =
                        $"There were less than 10 Product Impressions registered in the past three hours. ({report.Result.TotalItems})";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error has occured in ProductReportChecker.");
                _error = ex.Message;
                return false;
            }
        }

        public string GetErrorWarning()
        {
            return "Error found with hotspot informations: " + _error;
        }

        public bool IsScreenShotRequired => false;

        public byte[] Screenshot => throw new NotImplementedException();

        public string ServiceName => "Product Impressions";
    }
}