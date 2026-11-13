using TechStore.Interfaces;

namespace TechStore.Data.Seeders
{
    /// <summary>
    /// Extensiones para registrar y ejecutar el seed inicial del catálogo de productos y categorías.
    /// </summary>
    public static class CatalogSeedServiceExtensions
    {
        public static IServiceCollection AddCatalogSeeder(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddScoped<ICatalogSeeder, CatalogSeeder>();
            return services;
        }

        public static Task SeedCatalogAsync(
            this WebApplication app,
            bool resetExistingData = false,
            CancellationToken cancellationToken = default) =>
            app.Services.SeedCatalogAsync(resetExistingData, cancellationToken);

        public static async Task SeedCatalogAsync(
            this IServiceProvider services,
            bool resetExistingData = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(services);

            await using var scope = services.CreateAsyncScope();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("TechStore.Data.Seeders.CatalogSeedServiceExtensions");

            try
            {
                var seeder = scope.ServiceProvider.GetRequiredService<ICatalogSeeder>();
                await seeder.SeedAsync(resetExistingData, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ocurrió un error al ejecutar el seed inicial del catálogo.");
                throw;
            }
        }
    }
}
