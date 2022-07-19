using Microsoft.Extensions.DependencyInjection;
using SituSystems.ArtifactStore.Services.AutoMapper;

namespace SituSystems.SituTest.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSituTest(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(MappingProfile));
            services.AddMemoryCache();
            services.AddTransient<IUptimeChecker, UptimeChecker>();
            services.AddTransient<INotificationSender, NotificationSender>();

            return services;
        }
    }
}