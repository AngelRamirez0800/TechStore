namespace TechStore.DTOs.Admin
{
    public sealed class RoleSummaryDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsBuiltIn { get; init; }
        public int PermissionCount { get; init; }
        public int UserCount { get; init; }
        public IReadOnlyList<string> Permissions { get; init; } = [];
    }
}
