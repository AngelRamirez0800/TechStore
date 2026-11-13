namespace TechStore.Interfaces
{
    /// <summary>
    /// Contrato para inicializar de forma idempotente roles, permisos y usuarios base de Identity.
    /// </summary>
    public interface IIdentitySeeder
    {
        Task SeedAsync();
    }
}
