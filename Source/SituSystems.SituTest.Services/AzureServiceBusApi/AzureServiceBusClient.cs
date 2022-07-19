using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;
using SituSystems.SituTest.Contracts.Models;

namespace SituSystems.SituTest.Services.AzureServiceBusApi
{
    public class AzureServiceBusClient : IAzureServiceBusClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ServiceBusSettings _settings;

        public AzureServiceBusClient(
            IHttpClientFactory httpClientFactory,
            IOptions<ServiceBusSettings> settings)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
        }

        private HttpClient HttpClient => _httpClientFactory.GetAzureServiceBusHttpClient();

        public async Task<RenderTask> GetRenderTask(string topic, string subscription)
        {
            var request = $"https://{_settings.Url}/{topic}/subscriptions/{subscription}/messages/head";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, request);
            var authHeader = GenerateAuthHeader(topic, subscription);
            requestMessage.Headers.Add("Authorization", authHeader);
            var response = await HttpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var correlationId = GetCorrelationId(response.Headers);
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    LogContext.PushProperty("CorrelationId", correlationId);
                }

                return response.StatusCode != HttpStatusCode.NoContent
                    ? await response.Content.ReadFromJsonAsync<RenderTask>()
                    : null;
            }

            var failureBody = await response.Content.ReadAsStringAsync();
            Log.ForContext("Request", request)
                .ForContext("FailureBody", failureBody, destructureObjects: true)
                .Warning("AzureServiceBusClient GetRenderTask status code indicates failure: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);

            return null;
        }

        public async Task<string> GetTask(string topic, string subscription)
        {
            var request = $"https://{_settings.Url}/{topic}/subscriptions/{subscription}/messages/head";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, request);
            var authHeader = GenerateAuthHeader(topic, subscription);
            requestMessage.Headers.Add("Authorization", authHeader);
            var response = await HttpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var correlationId = GetCorrelationId(response.Headers);
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    LogContext.PushProperty("CorrelationId", correlationId);
                }

                return response.StatusCode != HttpStatusCode.NoContent
                    ? await response.Content.ReadAsStringAsync()
                    : null;
            }

            var failureBody = await response.Content.ReadAsStringAsync();
            Log.ForContext("Request", request)
                .ForContext("FailureBody", failureBody, destructureObjects: true)
                .Warning("AzureServiceBusClient GetTask status code indicates failure: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);

            return null;
        }

        public async Task<(T responseObject, string correlationId)> GetMessage<T>(string queue)
        {
            var request = $"https://{_settings.Url}/{queue}/messages/head";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, request);
            var authHeader = GenerateAuthHeader(null, null, queue);
            requestMessage.Headers.Add("Authorization", authHeader);
            var response = await HttpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                try
                {
                    var correlationId = GetCorrelationId(response.Headers);
                    if (!string.IsNullOrWhiteSpace(correlationId))
                    {
                        LogContext.PushProperty("CorrelationId", correlationId);
                    }

                    return response.StatusCode != HttpStatusCode.NoContent
                        ? (JsonConvert.DeserializeObject<T>(json), correlationId)
                        : (default(T), null);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            var failureBody = await response.Content.ReadAsStringAsync();
            Log.ForContext("Request", request)
                .ForContext("FailureBody", failureBody, destructureObjects: true)
                .Warning("AzureServiceBusClient GetMessage status code indicates failure: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);

            return (default(T), null);
        }

        private string GenerateAuthHeader(string topic, string subscription, string queue = "")
        {
            const string keyName = "RootManageSharedAccessKey";
            const int week = 60 * 60 * 24 * 7;

            var resourceUri = string.IsNullOrWhiteSpace(queue) ?
                $"{_settings.Url}/{topic}/Subscriptions/{subscription}" :
                $"{_settings.Url}/{queue}";
            var sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            var stringToSign = System.Net.WebUtility.UrlEncode(resourceUri) + "\n" + expiry;
            var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(_settings.Key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", System.Net.WebUtility.UrlEncode(resourceUri), System.Net.WebUtility.UrlEncode(signature), expiry, keyName);
        }

        private string GetCorrelationId(HttpResponseHeaders headers)
        {
            if (headers.TryGetValues("BrokerProperties", out var values))
            {
                var definition = new { CorrelationId = "" };
                var brokerProperties = JsonConvert.DeserializeAnonymousType(values.FirstOrDefault(), definition);

                return brokerProperties.CorrelationId;
            }

            return null;
        }
    }
}
