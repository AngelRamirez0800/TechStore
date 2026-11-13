namespace TechStore.DTOs.Admin
{
    public sealed class UserSummaryDto
    {
        private readonly bool? _isLockedOut;

        public string Id { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool EmailConfirmed { get; init; }
        public bool LockoutEnabled { get; init; }
        public DateTimeOffset? LockoutEnd { get; init; }

        public bool IsLockedOut
        {
            get => _isLockedOut ?? (LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow);
            init => _isLockedOut = value;
        }

        public int AccessFailedCount { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
    }
}
