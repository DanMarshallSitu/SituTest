using System;
using System.Net.Http;
using System.Threading.Tasks;
using CorrelationId.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SituSystems.Core.AzureServiceBus;
using SituSystems.Core.FileStorage;
using SituSystems.Core.Logging;
using SituSystems.Core.Security;
using SituSystems.Core.Security.Constants;
using SituSystems.Core.Security.Settings;
using SituSystems.KeyedArtifactStore.Contracts.Models;
using SituSystems.RenderService.Contracts.Models;
using SituSystems.RenderService.WebApi.Client.Contracts;
using SituSystems.SituHomeLauncher.Services;
using SituSystems.SituHomeLauncher.Services.AzureServiceBusApi;
using SituSystems.SituHomeLauncher.Services.Contract;
using SituSystems.Warp.Artifacts;

namespace SituSystems.SituHomeLauncher
{
    class Program
    {
        private static readonly string AppName = "SituHomeLauncher";

        static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(configureLogging => configureLogging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddCorrelationId();
                    AddLogging(services, hostContext);
                    services.AddMemoryCache();
                    services.AddAzureServiceBusClient();
                    AddServices(services, hostContext.Configuration);
                    services.AddTransient<IArtifactStoreService, ArtifactStoreService>();
                    services.AddSingleton<IGpuInfo, WmiGpuInfo>();
                    var appSettings = new AppSettings();
                    hostContext.Configuration.Bind("AppSettings", appSettings);
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));
                    services.Configure<WarpArtifactSettings>(hostContext.Configuration.GetSection("WarpSettings"));
                    AddServiceBus(services, hostContext.Configuration);

                    var warpSettings = hostContext.Configuration.GetSection("WarpSettings").Get<WarpArtifactSettings>();
                    services.AddWarpArtifactStore(warpSettings.DatabaseConnectionString, AppName, warpSettings);

                    AddTempIfcFileServices(hostContext, services);


                    services.AddHostedService<ArtifactStoreWorker>()
                        .Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "SituHome Launcher Service";
                            config.SourceName = "SituHome Launcher Service Source";
                        });
                });

        private static void AddLogging(IServiceCollection services, HostBuilderContext hostBuilderContext)
        {
            var loggerConfiguration = new LoggerConfiguration();
            LoggingHelpers.ConfigureLogger(hostBuilderContext.Configuration,
                loggerConfiguration,
                hostBuilderContext.Configuration.GetValue<string>("Environment"),
                hostBuilderContext.HostingEnvironment.EnvironmentName,
                AppName,
                hostBuilderContext.Configuration.GetValue<string>("AppVersion"),
                Environment.MachineName,
                hostBuilderContext.Configuration.GetValue<string>("ElasticSearchUri"),
                hostBuilderContext.Configuration.GetValue<string>("ElasticSearchApiKeyId"),
                hostBuilderContext.Configuration.GetValue<string>("ElasticSearchApiKey"),
                hostBuilderContext.Configuration.GetValue<string>("SendGridApiKey"),
                hostBuilderContext.Configuration.GetValue<string>("SendGridFromEmail"),
                hostBuilderContext.Configuration.GetValue<string>("SendGridToEmail"),
                hostBuilderContext.Configuration.GetValue<string>("SeqUri"),
                hostBuilderContext.Configuration.GetValue<string>("SeqApiKey"))
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Query", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Model.Validation", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning);

            var logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;

            services.AddLogging(lb => lb.AddSerilog(logger));
        }

        private static void AddServices(IServiceCollection services, IConfiguration configuration)
        {
            var situSecuritySettings = configuration.GetSection("SituSecurity").Get<SituSecuritySettings>();

            services.AddTransient<ITokenService>(provider =>
            {
                var identityServerBaseUrl = situSecuritySettings.IdentityServerBaseUrl;
                var clientId = ClientIds.SituView;
                var clientSecret = situSecuritySettings.Clients.SituView.ClientSecret;
                var scope = $"{SituScopes.SituAnalyticsWebApi} {SituScopes.IdentityServerWebApi} {SituScopes.RenderService}";

                return new TokenService(provider.GetService<IMemoryCache>(), identityServerBaseUrl, clientId, clientSecret, scope);
            });

            services.AddHttpClient();

            services.AddTransient<IRenderServiceClient>(provider =>
            {
                var httpClient = provider.GetService<IHttpClientFactory>().CreateClient("IRenderServiceClient");
                httpClient.Timeout = new TimeSpan(0, 7, 0);
                var baseUrl = situSecuritySettings.Clients.RenderService.BaseUrl;
                return RenderService.WebApi.Client.ClientFactory.CreateRenderServiceWebApiClient(baseUrl, httpClient, provider.GetService<ITokenService>().GetToken);
            });
        }

        private static void AddServiceBus(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Services.AzureServiceBusApi.ServiceBusSettings>(configuration.GetSection("ServiceBus"));
            var serviceBusSettings = new Services.AzureServiceBusApi.ServiceBusSettings();
            configuration.Bind("ServiceBus", serviceBusSettings);
            services.AddServiceBusMessaging();
            services.AddMessageSender<ArtifactFileAddedMessage>(serviceBusSettings.Topics.ArtifactFileAdded);
            services.AddMessageSender<RenderEvent>(serviceBusSettings.Topics.RenderEvent);
        }

        private static void AddTempIfcFileServices(HostBuilderContext builderContext, IServiceCollection services)
        {
            services.Configure<TempIfcFileStorageSettings>(builderContext.Configuration.GetSection("TempIfcFileStorageSettings"));

            services.AddTransient<ITempIfcFileService>(serviceProvider =>
            {
                var tempIfcFileStorageSettings = serviceProvider.GetService<IOptions<TempIfcFileStorageSettings>>()?.Value;
                if (tempIfcFileStorageSettings == null)
                {
                    Log.Error("There aren't any {Settings}", nameof(TempIfcFileStorageSettings));
                    throw new Exception($"There aren't any {nameof(TempIfcFileStorageSettings)}");
                }
                var tempIfcFileStorage = new AzureFileStorage(
                    tempIfcFileStorageSettings.AzureStorageCredentials.AccountName,
                    tempIfcFileStorageSettings.AzureStorageCredentials.AccountKey,
                    tempIfcFileStorageSettings.AzureStorageCredentials.ContainerName,
                    tempIfcFileStorageSettings.AzureStorageCredentials.ConnectionString);
                return new TempIfcFileService(tempIfcFileStorage, serviceProvider.GetService<IOptions<TempIfcFileStorageSettings>>());
            });

            services.AddTransient<ITempIfcFileQueueService, TempIfcFileQueueService>();
        }
    }
}
