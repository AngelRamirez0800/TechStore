using TechStore.DTOs.Admin;

namespace TechStore.Interfaces
{
    /// <summary>
    /// Contrato para administrar usuarios de Identity, asignación de roles y bloqueo de cuentas.
    /// </summary>
    public interface IUserManagementService
    {
        Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync();
        Task<UserSummaryDto?> GetUserByIdAsync(string userId);
        Task<UserRoleAssignmentInput?> GetUserRoleAssignmentAsync(string userId);
        Task<OperationResult> UpdateUserRolesAsync(UserRoleAssignmentInput input, string? currentAdminUserId = null);
        Task<OperationResult> ToggleLockoutAsync(string userId, string? currentAdminUserId = null);
    }
}
