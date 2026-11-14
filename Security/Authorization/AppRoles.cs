namespace TechStore.Security.Authorization
{
    /// <summary>
    /// Nombres canónicos de los roles base del sistema.
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Seller = "Seller";
        public const string Client = "Client";

        public static readonly IReadOnlyList<string> All = [Admin, Seller, Client];

        public static bool IsBuiltIn(string? roleName) =>
            !string.IsNullOrWhiteSpace(roleName) &&
            All.Any(role => string.Equals(role, roleName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
