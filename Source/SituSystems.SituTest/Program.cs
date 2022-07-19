using System;
using System.Threading.Tasks;
using CorrelationId.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;
using Serilog.Events;
using SituSystems.Core.Logging;
using SituSystems.Core.Security;
using SituSystems.Core.Security.Constants;
using SituSystems.Core.Security.Settings;
using SituSystems.SituTest.Services;

namespace SituSystems.SituTest
{
    class Program
    {
        private static readonly string AppName = "SituTest";

        static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(configureLogging => configureLogging
                .AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddCorrelationId();
                    AddLogging(services, hostContext);
                    services.AddMemoryCache();
                    AddServices(services, hostContext.Configuration);
                    var appSettings = new AppSettings();
                    hostContext.Configuration.Bind("AppSettings", appSettings);
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));

                    services.AddHostedService<SituTestWorker>()
                        .Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "SituTest Service";
                            config.SourceName = "SituTest Service Source";
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
            services.AddTransient<INotificationSender, NotificationSender>();
            services.AddTransient<IUptimeChecker, UptimeChecker>();

            services.AddTransient<ITokenService>(provider =>
            {
                var identityServerBaseUrl = situSecuritySettings.IdentityServerBaseUrl;
                var clientId = ClientIds.SituView;
                var clientSecret = situSecuritySettings.Clients.SituView.ClientSecret;
                var scope = $"{SituScopes.SituAnalyticsWebApi} {SituScopes.IdentityServerWebApi} {SituScopes.RenderService}";

                return new TokenService(provider.GetService<IMemoryCache>(), identityServerBaseUrl, clientId, clientSecret, scope);
            });

            services.AddHttpClient();
        }
    }
}
