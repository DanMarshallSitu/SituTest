using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SituSystems.SituTest.Services.ServiceCheckers;

namespace SituSystems.SituTest.Services
{
    public class SituTestWorker : BackgroundService
    {
        private readonly ILogger<SituTestWorker> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly AppSettings _settings;

        public SituTestWorker(
            ILogger<SituTestWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<AppSettings> settings)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
                try
                {
                    var start = DateTime.UtcNow;

                    using var scope = _serviceScopeFactory.CreateScope();
                    var uptimeChecker = scope.ServiceProvider.GetService<IUptimeChecker>();
                    await uptimeChecker!.Run(GetServiceCheckers(_settings));
                    while (start.AddSeconds(_settings.CheckPeriodInSeconds) < DateTime.UtcNow)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ArtifactStoreWorker Error");
                    var delay = TimeSpan.FromSeconds(_settings.UnhandledExceptionWaitInSeconds);
                    await Task.Delay(delay, stoppingToken);
                }

            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            await tcs.Task;

            _logger.LogInformation("Service stopped");
        }

        private static List<ServiceChecker> GetServiceCheckers(AppSettings appSettings)
        {
            var serviceCheckerFactory = new PanoramaCheckerFactory(appSettings);

            return new List<ServiceChecker> 
            { 
                serviceCheckerFactory.BurbankPanoramaChecker, 
                serviceCheckerFactory.SituDemoPanoramaChecker
            };
        }
    }
}
