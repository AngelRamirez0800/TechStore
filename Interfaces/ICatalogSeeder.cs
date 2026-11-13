namespace TechStore.Interfaces
{
    /// <summary>
    /// Contrato para inicializar de forma idempotente el catálogo base de categorías, productos e imágenes reales.
    /// </summary>
    public interface ICatalogSeeder
    {
        Task SeedAsync(bool resetExistingData = false, CancellationToken cancellationToken = default);
    }
}
