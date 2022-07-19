using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SituSystems.SituTest.Services;

namespace SituSystems.SituTest
{
    public class SituTestWorker : BackgroundService
    {
        private readonly ILogger<SituTestWorker> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly List<DateTime> _errors = new();

        public SituTestWorker(
            ILogger<SituTestWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var uptimeChecker = scope.ServiceProvider.GetService<IUptimeChecker>();

                    await uptimeChecker!.Run();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ArtifactStoreWorker Error");

                    //clean out old errors first
                    _errors.RemoveAll(x => x < DateTime.UtcNow.AddHours(-6));
                    _errors.Add(DateTime.UtcNow);

                    // wait at least 1 minute before trying again
                    var minsToWait = 1 + _errors.Count / 2;
                    await Task.Delay(new TimeSpan(0, minsToWait, 0), stoppingToken);
                }

            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>) s).SetResult(true), tcs);
            await tcs.Task;

            _logger.LogInformation("Service stopped");
        }
    }
}