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
    internal class Program
    {
        private static readonly string AppName = "SituTest";

        private static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(configureLogging => configureLogging
                    .AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))

                .ConfigureServices((hostContext, services) => ConfigureServices(services, hostContext));
        }

        private static void ConfigureServices(IServiceCollection services, HostBuilderContext hostContext)
        {
            services.AddCorrelationId();
            AddLogging(services, hostContext);
            services.AddMemoryCache();
            AddServices(services, hostContext.Configuration);
            hostContext.Configuration.Bind("AppSettings", new AppSettings());
            services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));
            services.AddHostedService<SituTestWorker>()
                .Configure<EventLogSettings>(config =>
                {
                    config.LogName = "SituTest Service";
                    config.SourceName = "SituTest Service Source";
                });
        }

        private static void AddLogging(IServiceCollection services, HostBuilderContext builder)
        {
            var loggerConfiguration = new LoggerConfiguration();

            string GetValue(string key)
            {
                return builder.Configuration.GetValue<string>(key);
            }

            LoggingHelpers.ConfigureLogger(builder.Configuration,
                    loggerConfiguration,
                    GetValue("Environment"),
                    builder.HostingEnvironment.EnvironmentName,
                    AppName,
                    GetValue("AppVersion"),
                    Environment.MachineName,
                    GetValue("ElasticSearchUri"),
                    GetValue("ElasticSearchApiKeyId"),
                    GetValue("ElasticSearchApiKey"),
                    GetValue("SendGridApiKey"),
                    GetValue("SendGridFromEmail"),
                    GetValue("SendGridToEmail"),
                    GetValue("SeqUri"),
                    GetValue("SeqApiKey"))
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
            services.AddTransient<IUptimeChecker, UptimeChecker>();

            services.AddTransient<ITokenService>(provider =>
            {
                var identityServerBaseUrl = situSecuritySettings.IdentityServerBaseUrl;
                var clientId = ClientIds.SituView;
                var clientSecret = situSecuritySettings.Clients.SituView.ClientSecret;
                var scope =
                    $"{SituScopes.SituAnalyticsWebApi} {SituScopes.IdentityServerWebApi} {SituScopes.RenderService}";

                return new TokenService(provider.GetService<IMemoryCache>(),
                    identityServerBaseUrl,
                    clientId,
                    clientSecret,
                    scope);
            });

            services.AddHttpClient();
        }
    }
}