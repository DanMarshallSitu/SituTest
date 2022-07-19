using System;
using System.Text.Json;
using System.Collections.Generic;
using SituSystems.SituHomeLauncher.Services.Contract;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;
using SituSystems.ArtifactStore.Contracts.Queries;
using SituSystems.Core.AzureServiceBus;
using SituSystems.KeyedArtifactStore.Contracts.Commands;
using SituSystems.KeyedArtifactStore.Contracts.Models;
using SituSystems.KeyedArtifactStore.Services;
using SituSystems.RenderService.Contracts.Commands;
using SituSystems.RenderService.Contracts.Models;
using SituSystems.RenderService.Contracts.Models.Enums;
using SituSystems.RenderService.WebApi.Client.Contracts;
using SituSystems.SituHomeLauncher.Services.AzureServiceBusApi;
using SituSystems.Warp.Artifacts;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using SituSystems.RenderService.Contracts.Queries;
using Serilog.Context;
using CorrelationId.Abstractions;
using CorrelationId;
using SituSystems.ArtifactStore.Contracts.Models;

namespace SituSystems.SituHomeLauncher.Services
{
    public class ArtifactStoreService : IArtifactStoreService
    {
        private readonly IAzureServiceBusClient _azureServiceBusClient;
        private readonly IOptions<WarpArtifactSettings> _warpSettings;
        private readonly IKeyedFileSystemService _keyedFileSystemService;
        private readonly IRecipeService _recipeService;
        private readonly IRenderServiceClient _renderServiceClient;
        private readonly ArtifactStore.Services.IArtifactStoreService _artifactStoreService;
        private readonly IMemoryCache _cache;
        private readonly IMessageSender<RenderEvent> _renderEventMessageSender;
        private readonly ICorrelationContextFactory _correlationContextFactory;
        private readonly IGpuInfo _gpuInfo;
        private readonly string _instanceName;
        private readonly ITempIfcFileQueueService _tempIfcFileQueueService;

        private RendererHealthcheckRequest _healthcheckRequest;
        private ProcessRecipeRequest _processRecipeRequestMessage;
        private TempIfcFileMessage _tempIfcFileMessage;
        private DateTime _lastTimeHealthChecked = DateTime.UtcNow;
        private readonly string _bimViewerReferenceKey = @"software\bimviewer";
        private readonly string _bimViewerCacheKey = "bimviewer";
        private readonly string _rendererConfigCacheKey = "rendererConfig";
        private readonly string _launcherVersion;
        private readonly string _launcherEnvironment;
        private string _correlationId;

        private readonly AzureServiceBusApi.ServiceBusSettings _serviceBusSettings;

        public ArtifactStoreService(
            IConfiguration configuration,
            IAzureServiceBusClient azureServiceBusClient,
            IKeyedFileSystemService keyedFileSystemService,
            IRecipeService recipeService,
            IRenderServiceClient renderServiceClient,
            ArtifactStore.Services.IArtifactStoreService artifactStoreService,
            IMemoryCache cache,
            IOptions<WarpArtifactSettings> warpSettings,
            IOptions<AppSettings> appSettings,
            IMessageSender<RenderEvent> renderEventMessageSender,
            ICorrelationContextFactory correlationContextFactory,
            IGpuInfo gpuInfo,
            ITempIfcFileQueueService tempIfcFileQueueService,
            IOptions<AzureServiceBusApi.ServiceBusSettings> serviceBusSettings)
        {
            _azureServiceBusClient = azureServiceBusClient;
            _keyedFileSystemService = keyedFileSystemService;
            _recipeService = recipeService;
            _renderServiceClient = renderServiceClient;
            _artifactStoreService = artifactStoreService;
            _cache = cache;
            _warpSettings = warpSettings;
            _instanceName = appSettings.Value.InstanceName;
            _renderEventMessageSender = renderEventMessageSender;
            _launcherVersion = configuration.GetValue<string>("AppVersion");
            _launcherEnvironment = configuration.GetValue<string>("Environment");
            _correlationContextFactory = correlationContextFactory;
            _gpuInfo = gpuInfo;
            _tempIfcFileQueueService = tempIfcFileQueueService;
            _serviceBusSettings = serviceBusSettings.Value;
        }

        public async Task CheckForNextRecipeToCook()
        {
            var rendererConfig = await GetRendererConfig();
            await Task.Delay(TimeSpan.FromMilliseconds(rendererConfig.QueueCheckIntervalInMilliseconds));

            foreach (var queue in rendererConfig.Queues.Where(x => x.Enabled))
            {
                if (queue.Name.ToLower() == _serviceBusSettings.Queues?.TempIfcFiles)
                {
                    var messageResult = await _azureServiceBusClient.GetMessage<TempIfcFileMessage>(queue.Name);
                    if (messageResult.responseObject != null)
                    {
                        _correlationId = messageResult.correlationId;
                        _correlationContextFactory.Create(_correlationId, CorrelationIdOptions.DefaultHeader);
                        _tempIfcFileMessage = messageResult.responseObject;
                        if (_tempIfcFileMessage != null) break;
                    };
                }
                else
                {
                    var messageResult = await _azureServiceBusClient.GetMessage<ProcessRecipeRequest>(queue.Name);
                    if (messageResult.responseObject != null)
                    {
                        _correlationId = messageResult.correlationId;
                        _correlationContextFactory.Create(_correlationId, CorrelationIdOptions.DefaultHeader);
                        _processRecipeRequestMessage = messageResult.responseObject;
                        if (_processRecipeRequestMessage != null) break;
                    };
                }
            }

            if (_processRecipeRequestMessage == null && _tempIfcFileMessage == null)
            {
                await SendRendererHealthCheck();
                return;
            }

            if (_tempIfcFileMessage != null)
            {
                await ProcessTempIfcFile();
            }
            else if (_processRecipeRequestMessage != null)
            {
                await ProcessRecipeRequest();
            }
        }

        private async Task ProcessRecipeRequest()
        {
            using (LogContext.PushProperty("CorrelationId", _correlationId))
            {
                if (_processRecipeRequestMessage == null)
                {
                    Log.Warning("Failed: ProcessRecipeRequestMessage is null {Instance}", _instanceName);
                    return;
                }

                var errors = new List<string>();
                try
                {
                    await SendRendererHealthCheck();
                    var recipeResponse = _recipeService.GetRecipe(_processRecipeRequestMessage);

                    if (recipeResponse.Success)
                    {
                        var recipe = recipeResponse.ArtifactRecipe;

                        while (!recipe.IsComplete())
                        {
                            await SendRendererHealthCheck();
                            var job = _recipeService.SelectJobToRun(recipe);
                            if (job == null)
                            {
                                await ProcessRenderError(errors);
                                Log.Warning("Failed: Unable to select a job when creating {ResultKey} {Instance}", recipe.ArtifactToCreate.GetCallerReferenceKey(), _instanceName);
                                break;
                            }

                            await AddRenderEvent(recipe, job, _processRecipeRequestMessage.ArtifactId, RenderEventType.Beginning, _processRecipeRequestMessage.BaseIngredients);
                            Log.Information("Running Job: {@Job} {Instance}", job, _instanceName);
                            var jobResponse = _keyedFileSystemService.RunJob(GetRunJobRequest(job, recipe));

                            if (jobResponse.Success)
                            {
                                Log.Information("Job succeeded: {@Job} {Instance}", job, _instanceName);
                                _recipeService.RegisterJobComplete(recipe, job, jobResponse.Result);
                            }
                            else
                            {
                                errors.AddRange(jobResponse.ErrorMessages.Select(x => $"{x} : {job.ResultKey.GetCallerReferenceKey()}").ToList());
                                Log.Error("Job Failed! {SoftWare} {@Job} {Errors} when creating {ResultKey} {Instance}",
                                    string.Join("-", job.JobStages.Select(x => x.Executable.Replace(".exe", "").Replace(".bat", "")).ToList()),
                                    job, string.Join(",", jobResponse.ErrorMessages), job.ResultKey.GetCallerReferenceKey(), _instanceName);
                                _recipeService.RegisterJobFailed(recipe, job);
                                await AddRenderEvent(recipe, job, _processRecipeRequestMessage.ArtifactId, RenderEventType.Middle, _processRecipeRequestMessage.BaseIngredients, $"Error: {string.Join(",", jobResponse.ErrorMessages)}");
                            }
                            await AddRenderEvent(recipe, job, _processRecipeRequestMessage.ArtifactId, RenderEventType.End, _processRecipeRequestMessage.BaseIngredients);
                        }

                        if (recipe.IsComplete())
                        {
                            Log.Information("Recipe Completed : Result version is {Version} {Instance}", recipe?.VersionCreated.GetValueOrDefault(), _instanceName);
                        }
                        else
                        {
                            if (recipe.Failed)
                            {
                                Log.Error("Recipe Failed! Failures: {@Failures} when Creating {ResultKey} {Instance} {@Errors}", recipe.JobFailures, recipe.ArtifactToCreate.GetCallerReferenceKey(), _instanceName, errors);
                            }
                        }

                        await SendRendererHealthCheck();
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to process jobs {ArtifactReferenceKey} {Instance}", _processRecipeRequestMessage.ArtifactToCreate, _instanceName);
                }
                finally
                {
                    _processRecipeRequestMessage = null;
                    _correlationId = null;
                    _correlationContextFactory.Create(_correlationId, CorrelationIdOptions.DefaultHeader);
                }
            }
        }

        private RunJobRequest GetRunJobRequest(Job job, Recipe recipe)
        {
            if (job == null || recipe == null || _processRecipeRequestMessage.ArtifactId == Guid.Empty)
            {
                return new RunJobRequest()
                {
                    Job = job,
                    LocalCacheRootPath = _warpSettings.Value.LocalCacheRootPath
                };
            }

            return new RunJobRequest()
            {
                Job = job,
                LocalCacheRootPath = _warpSettings.Value.LocalCacheRootPath,
                ArtifactId = _recipeService.IsFinalJob(recipe, job) ? _processRecipeRequestMessage.ArtifactId : null
            };
        }

        private async Task ProcessTempIfcFile()
        {
            using (LogContext.PushProperty("CorrelationId", _correlationId))
            {
                if (_tempIfcFileMessage == null)
                {
                    Log.Warning("Failed: ProcessTempIfcFile is null {Instance}", _instanceName);
                    return;
                }

                Log.Information("Processing new temp ifc file {FileName} for home {HomeName} {Instance}", _tempIfcFileMessage.FileName, _tempIfcFileMessage.HomeName, _instanceName);

                var errors = new List<string>();
                try
                {
                    await _tempIfcFileQueueService.ProcessTempIfcFileAsync(_tempIfcFileMessage);

                    Log.Information("Processed temp ifc file {FileName} for home {HomeName} {Instance}", _tempIfcFileMessage.FileName, _tempIfcFileMessage.HomeName, _instanceName);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to process temp ifc file {FileName} for home {HomeName} {Instance}", _tempIfcFileMessage.FileName, _tempIfcFileMessage.HomeName, _instanceName);
                }
                finally
                {
                    _correlationId = null;
                    _correlationContextFactory.Create(_correlationId, CorrelationIdOptions.DefaultHeader);
                }
            }
        }

        private async Task SendRendererHealthCheck()
        {
            try
            {
                var pathRoot = Path.GetPathRoot(_warpSettings.Value.LocalCacheRootPath);

                // Get Drive information.
                var allDrives = DriveInfo.GetDrives().ToList();
                var drive = allDrives.ToList().First(x => string.Equals(x.Name, pathRoot, StringComparison.CurrentCultureIgnoreCase));
                var gpuNames = GetGpuNames();

                var metaData = new Dictionary<string, string>
                {
                    {"discSpaceUsed", (drive.TotalSize - drive.AvailableFreeSpace).ToString()},
                    {"discSpaceTotal", drive.TotalSize.ToString()},
                    {"appVersion", _launcherVersion},
                    {"appEnvironment", _launcherEnvironment},
                    {"processingQueues", ""},
                    {"gpuNames", gpuNames }
                };
                if (_processRecipeRequestMessage != null)
                {
                    metaData.Add("currentRequest", JsonSerializer.Serialize(_processRecipeRequestMessage, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                }

                var bimViewerVersion = GetArtifactVersion();
                var healthcheckRequest = new RendererHealthcheckRequest
                {
                    LastHealthyAt = DateTime.UtcNow,
                    Name = _instanceName,
                    Version = $"{bimViewerVersion.Artifact.CallerReferenceKey}\\{bimViewerVersion.Artifact.Version}",
                    CurrentTask = _processRecipeRequestMessage?.Id.ToString(),
                    MetaData = metaData
                };

                if (IsEqual(_healthcheckRequest, healthcheckRequest) && _lastTimeHealthChecked < DateTime.UtcNow) return;

                _healthcheckRequest = healthcheckRequest;
                await _renderServiceClient.RendererHealthcheckAsync(_healthcheckRequest);
            }
            catch (Exception e)
            {
                Log.Information($"Failed to Health Check {e.Message}.");
            }
            finally
            {
                _lastTimeHealthChecked = DateTime.UtcNow.AddSeconds(30);
            }
        }

        private string GetGpuNames()
        {
            var info = _gpuInfo.GetInfo();

            return string.Join(", ", info.Select(i => GetGpuDescription(i)));

            static string GetGpuDescription((VideoController VideoController, string ErrorMessage) i)
                => string.IsNullOrEmpty(i.ErrorMessage) ? i.VideoController.Name : i.ErrorMessage;
        }

        private bool IsEqual(RendererHealthcheckRequest a, RendererHealthcheckRequest b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.CurrentTask != b.CurrentTask) return false;
            if (a.Name != b.Name) return false;
            if (a.Ip != b.Ip) return false;
            if (a.Version != b.Version) return false;

            var dictionariesEqual = a.MetaData.Keys.Count == b.MetaData.Keys.Count && a.MetaData.Keys.All(k => b.MetaData.ContainsKey(k) && Equals(a.MetaData[k], b.MetaData[k]));
            return dictionariesEqual;
        }

        private async Task AddRenderEvent(Recipe recipe, Job job, Guid? artifactId, RenderEventType renderEventType, List<ArtifactVersionReference> baseIngredients, string eventOverride = "")
        {
            var renderEvent = string.IsNullOrWhiteSpace(eventOverride) ?
                (renderEventType == RenderEventType.Beginning ? "Job has begun." : "Job has ended.") :
                eventOverride;
            try
            {
                await _renderEventMessageSender.SendAsync(new RenderEvent
                {
                    Id = Guid.NewGuid(),
                    ArtifactKey = job.ResultKey.ArtifactType(),
                    ArtifactId = artifactId,
                    JobArtifactKey = job.ResultKey.GetCallerReferenceKey(),
                    RecipeArtifactKey = recipe.ArtifactToCreate.GetCallerReferenceKey(),
                    RenderEventType = renderEventType,
                    Event = renderEvent,
                    RenderMachine = _instanceName,
                    RecipeFingerprint = await _recipeService.GetBaseIngredientsFingerprint(baseIngredients),
                    DateTimeStamp = DateTime.UtcNow
                }, null, Guid.NewGuid().ToString(), _correlationId);
            }
            catch (Exception e)
            {
                Log.Information($"Failed to Add Render Event {e.Message}");
            }
        }

        private GetArtifactResponse GetArtifactVersion()
        {
            var bimViewerVersion = _cache.Get<GetArtifactResponse>(_bimViewerCacheKey);
            if (bimViewerVersion != null) return bimViewerVersion;
            bimViewerVersion = _artifactStoreService.GetArtifactLatestVersion(_bimViewerReferenceKey);
            _cache.Set(_bimViewerCacheKey, bimViewerVersion, DateTimeOffset.UtcNow.AddMinutes(1));
            return bimViewerVersion;
        }

        private async Task<RendererConfig> GetRendererConfig()
        {
            var rendererConfig = _cache.Get<RendererConfig>(_rendererConfigCacheKey);
            if (rendererConfig != null) return rendererConfig;
            var getRendererConfigResponse = await _renderServiceClient.GetRendererConfigAsync(new GetRendererConfigRequest { RendererName = _instanceName });
            if (!getRendererConfigResponse.Success)
            {
                Log.Error("Launcher could not retrieve config! {Errors}", string.Join(", ", getRendererConfigResponse.ErrorMessages));
                throw new Exception($"Launcher could not retrieve config! {string.Join(", ", getRendererConfigResponse.ErrorMessages)}");
            }
            rendererConfig = getRendererConfigResponse.RendererConfig;
            rendererConfig.Queues = rendererConfig.Queues.OrderBy(x => x.Priority).ToList();
            _cache.Set(_rendererConfigCacheKey, rendererConfig, DateTimeOffset.UtcNow.AddMinutes(2));
            return rendererConfig;
        }

        private async Task ProcessRenderError(IEnumerable<string> errors)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_processRecipeRequestMessage?.ArtifactToCreate))
                {
                    await _renderServiceClient.SaveRenderErrorAsync(new SaveRenderErrorRequest
                    {
                        ArtifactToCreate = _processRecipeRequestMessage.ArtifactToCreate,
                        BaseIngredients = _processRecipeRequestMessage.BaseIngredients,
                        ErrorMessage = string.Join(", ", errors)
                    });
                }
            }
            catch (Exception e)
            {
                Log.Information($"Failed to Health Check {e.Message}.");
            }
        }
    }
}
