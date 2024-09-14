using Document_library.Services.Implementations;

namespace Document_library.Configuration
{
    public static class CustomServiceExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            // Register IS3Service as Scoped (new instance per request)
            services.AddScoped<IS3Service, S3Service>();

            // Register ILoggerService as Singleton (one instance for the application's lifetime)
            services.AddSingleton<ILoggerService, LoggerService>();

            // Register IAccountService as Scoped
            services.AddScoped<IAccountService, AccountService>();

            return services;
        }
    }
}
