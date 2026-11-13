namespace TechStore.DTOs.Admin
{
    public sealed class RoleDetailsDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsBuiltIn { get; init; }
        public IReadOnlyList<string> Permissions { get; init; } = [];
        public IReadOnlyList<UserSummaryDto> Users { get; init; } = [];
    }
}
