using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SituSystems.SituHomeLauncher.Services.AzureServiceBusApi
{
    public static class AzureServiceBusExtensions
    {
        private const string Name = "AzureServiceBus";

        public static HttpClient GetAzureServiceBusHttpClient(this IHttpClientFactory httpClientFactory)
        {
            return httpClientFactory.CreateClient(Name);
        }

        public static IServiceCollection AddAzureServiceBusClient(this IServiceCollection services)
        {
            services.AddAzureServiceBusHttpClient();
            services.AddSingleton<IAzureServiceBusClient, AzureServiceBusClient>();
            return services;
        }

        private static IHttpClientBuilder AddAzureServiceBusHttpClient(this IServiceCollection services)
        {
            return services.AddHttpClient(Name);
        }
    }
}
