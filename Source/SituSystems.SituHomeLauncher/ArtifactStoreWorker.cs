using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SituSystems.SituHomeLauncher.Services.Contract;

namespace SituSystems.SituHomeLauncher
{
    public class ArtifactStoreWorker : BackgroundService
    {
        private readonly ILogger<ArtifactStoreWorker> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private List<DateTime> _errors = new List<DateTime>();

        public ArtifactStoreWorker(
            ILogger<ArtifactStoreWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var artifactStoreService = scope.ServiceProvider.GetService<IArtifactStoreService>();

                    await artifactStoreService.CheckForNextRecipeToCook();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ArtifactStoreWorker Error");

                    //clean out old errors first
                    _errors.RemoveAll(x => x < DateTime.UtcNow.AddHours(-6));
                    _errors.Add(DateTime.UtcNow);

                    // wait at least 1 minute before trying again
                    var minsToWait = 1 + (_errors.Count / 2);
                    await Task.Delay(new TimeSpan(0, minsToWait, 0), stoppingToken);
                }
            }

            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            await tcs.Task;

            _logger.LogInformation("Service stopped");
        }
    }
}
