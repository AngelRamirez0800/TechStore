using TechStore.DTOs.Admin;

namespace TechStore.Interfaces
{
    /// <summary>
    /// Contrato para administrar roles de Identity y sus permisos asociados mediante Role Claims.
    /// </summary>
    public interface IRoleManagementService
    {
        Task<IReadOnlyList<RoleSummaryDto>> GetAllRolesAsync();
        Task<RoleDetailsDto?> GetRoleByIdAsync(string roleId);
        Task<OperationResult> CreateRoleAsync(RoleInput input);
        Task<OperationResult> UpdateRoleAsync(string roleId, RoleInput input);
        Task<OperationResult> UpdateRolePermissionsAsync(RolePermissionsInput input);
        Task<OperationResult> DeleteRoleAsync(string roleId);
    }
}
