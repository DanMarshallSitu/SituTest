using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

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
                    
                    await uptimeChecker!.Run();

                    while (start.AddSeconds(_settings.CheckPeriodInSeconds) < DateTime.UtcNow)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ArtifactStoreWorker Error");

                    await Task.Delay(TimeSpan.FromSeconds(_settings.UnhandledExceptionWaitInSeconds), stoppingToken);
                }

            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>) s).SetResult(true), tcs);
            await tcs.Task;

            _logger.LogInformation("Service stopped");
        }
    }
}