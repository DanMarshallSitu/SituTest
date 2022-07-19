using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture.Xunit2;
using CorrelationId;
using CorrelationId.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SituSystems.ArtifactStore.Contracts.Models;
using SituSystems.Core.AzureServiceBus;
using SituSystems.KeyedArtifactStore.Contracts.Commands;
using SituSystems.KeyedArtifactStore.Contracts.Queries;
using SituSystems.KeyedArtifactStore.Services;
using SituSystems.RenderService.Contracts.Models;
using SituSystems.RenderService.Contracts.Models.Enums;
using SituSystems.RenderService.Contracts.Queries;
using SituSystems.RenderService.WebApi.Client.Contracts;
using SituSystems.SituHomeLauncher.Services;
using SituSystems.SituHomeLauncher.Services.AzureServiceBusApi;
using SituSystems.Warp.Artifacts;
using Xunit;

namespace SituSystems.SituHomeLauncher.Tests.Unit.Services
{
    public class ArtifactStoreServiceTests
    {
        [Theory, AutoMoqData]
        public async Task CheckForNextRecipeToCook_WhenCanNotRetrieveRendererConfig_ShouldThrowException(
            [Frozen] Mock<IRenderServiceClient> renderServiceClient,
            [Frozen] Mock<IMemoryCache> cache,
            ArtifactStoreService sut)
        {
            object expectedValue = null;
            MockMemoryCache(cache, expectedValue, false);
            renderServiceClient.Setup(x => x.GetRendererConfigAsync(It.IsAny<GetRendererConfigRequest>())).Returns(Task.FromResult(new GetRendererConfigResponse
            {
                Success = false,
                ErrorMessages = new List<string>{ "No Config" }
            }));

            await Assert.ThrowsAsync<Exception>(() => sut.CheckForNextRecipeToCook());
        }

        [Theory, AutoMoqData]
        public async Task CheckForNextRecipeToCook_WhenConfigExistsInCacheButNoEnabledQueues_ShouldSendHealthCheckAndBail(
            [Frozen] Mock<IRecipeService> recipeService,
            [Frozen] Mock<IMemoryCache> cache,
            ArtifactStoreService sut)
        {
            RendererConfig config = GetRendererConfig();
            object expectedValue = config;
            MockMemoryCache(cache, expectedValue, true);

            await sut.CheckForNextRecipeToCook();

            recipeService.Verify(x => x.GetRecipe(It.IsAny<GetRecipeRequest>()), Times.Never, "We should never make it to GetRecipe in the RecipeService");
        }

        [Theory, AutoMoqData]
        public async Task CheckForNextRecipeToCook_WhenNoCacheAndRetrieveFromRenderServiceButNoEnabledQueues_ShouldSendHealthCheckAndBail(
            [Frozen] Mock<IRecipeService> recipeService,
            [Frozen] Mock<IRenderServiceClient> renderServiceClient,
            [Frozen] Mock<IMemoryCache> cache,
            ArtifactStoreService sut)
        {
            RendererConfig noEnabledQueues = GetRendererConfig();
            object expectedValue = null;
            MockMemoryCache(cache, expectedValue, true);
            renderServiceClient.Setup(x => x.GetRendererConfigAsync(It.IsAny<GetRendererConfigRequest>())).Returns(Task.FromResult(new GetRendererConfigResponse
            {
                Success = true,
                RendererConfig = noEnabledQueues
            }));

            await sut.CheckForNextRecipeToCook();

            renderServiceClient.Verify(x => x.GetRendererConfigAsync(It.IsAny<GetRendererConfigRequest>()), Times.Once);
            recipeService.Verify(x => x.GetRecipe(It.IsAny<GetRecipeRequest>()), Times.Never, "We should never make it to GetRecipe in the RecipeService");
        }

        //TODO: Revisit when we begin the VR Integration
        //[Theory]
        //[InlineAutoMoqData(RenderType.PathTraced)]
        //[InlineAutoMoqData(RenderType.Rasterized)]
        //public async Task CheckForNextRecipeToCook_WhenCompleteRecipe_ShouldBail(
        //    RenderType renderType,
        //    [Frozen] Mock<IAzureServiceBusClient> serviceBusClient,
        //    [Frozen] Mock<IOptions<WarpArtifactSettings>> fileStorageSettings,
        //    [Frozen] Mock<IKeyedFileSystemService> keyedFileSystemService,
        //    [Frozen] Mock<IRecipeService> recipeService,
        //    [Frozen] Mock<IRenderServiceClient> renderServiceClient,
        //    [Frozen] Mock<ArtifactStore.Services.IArtifactStoreService> warpArtifactStoreService,
        //    [Frozen] Mock<IMemoryCache> cache,
        //    [Frozen] Mock<IMessageSender<RenderEvent>> renderEventMessageSender,
        //    [Frozen] Mock<ICorrelationContextFactory> correlationContextFactory,
        //    ArtifactStoreService sut)
        //{
        //    RendererConfig config = GetRendererConfig(true);
        //    object expectedValue = config;
        //    MockMemoryCache(cache, expectedValue, true);
        //    correlationContextFactory.Setup(x => x.Create(Guid.NewGuid().ToString(), CorrelationIdOptions.DefaultHeader));
        //    serviceBusClient.Setup(x => x.GetMessage<ProcessRecipeRequest>(config.Queues.FirstOrDefault().Name)).Returns(Task.FromResult((
        //        new ProcessRecipeRequest
        //        {
        //            ArtifactToCreate = "",
        //            BaseIngredients = new List<ArtifactVersionReference>(),
        //            Id = Guid.NewGuid()
        //        },
        //        Guid.NewGuid().ToString())
        //    ));

        //    await sut.CheckForNextRecipeToCook();

        //    recipeService.Verify(x => x.GetRecipe(It.IsAny<GetRecipeRequest>()), Times.Once);
        //}

        private static void MockMemoryCache(Mock<IMemoryCache> memoryCache, object expectedValue, bool result)
        {
            memoryCache
                .Setup(x => x.TryGetValue(It.IsAny<object>(), out expectedValue))
                .Returns(result);
        }

        private static RendererConfig GetRendererConfig(bool queueEnabled = false)
        {
            return new RendererConfig
            {
                QueueCheckIntervalInMilliseconds = 1,
                Queues = new List<QueueConfig>
                {
                    new QueueConfig
                    {
                        Enabled = queueEnabled,
                        Name = "test-queue",
                        Priority = 0
                    }
                }
            };
        }

        //TODO: FOR OTHER TESTS WHERE MORE SERVICES ARE NEEDED
        //[Theory, AutoMoqData]
        //public async Task CheckForNextRecipeToCook_WhenCanNotRetrieveRendererConfig_ShouldThrowException(
        //    [Frozen] Mock<IAzureServiceBusClient> serviceBusClient,
        //    [Frozen] Mock<IOptions<WarpArtifactSettings>> fileStorageSettings,
        //    [Frozen] Mock<IKeyedFileSystemService> keyedFileSystemService,
        //    [Frozen] Mock<IRecipeService> recipeService,
        //    [Frozen] Mock<IRenderServiceClient> renderServiceClient,
        //    [Frozen] Mock<ArtifactStore.Services.IArtifactStoreService> warpArtifactStoreService,
        //    [Frozen] Mock<IMemoryCache> cache,
        //    [Frozen] Mock<IMessageSender<RenderEvent>> renderEventMessageSender,
        //    [Frozen] Mock<ICorrelationContextFactory> correlationContextFactory,
        //    ArtifactStoreService sut)
        //{
        //    RendererConfig config = null;
        //    object expectedValue = config;
        //    MockMemoryCacheService.MockMemoryCache(expectedValue);
        //    renderServiceClient.Setup(x => x.GetRendererConfigAsync(It.IsAny<GetRendererConfigRequest>())).Returns(Task.FromResult(new GetRendererConfigResponse
        //    {
        //        Success = false,
        //        ErrorMessages = new List<string> { "No Config" }
        //    }));

        //    await Assert.ThrowsAsync<Exception>(() => sut.CheckForNextRecipeToCook());
        //}
    }
}
