using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechStore.Constants;
using TechStore.DTOs.Admin;
using TechStore.Interfaces;

namespace TechStore.Services
{
    /// <summary>
    /// Servicio de administración CRUD de roles y sus permisos basados en Role Claims.
    /// </summary>
    public sealed class RoleManagementService : IRoleManagementService
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RoleManagementService> _logger;

        public RoleManagementService(
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager,
            ILogger<RoleManagementService> logger)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<RoleSummaryDto>> GetAllRolesAsync()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            var summaries = new List<RoleSummaryDto>(roles.Count);
            foreach (var role in roles)
            {
                var roleName = role.Name ?? string.Empty;
                var permissions = await GetRolePermissionKeysAsync(role);
                IList<IdentityUser> users = string.IsNullOrWhiteSpace(roleName)
                    ? []
                    : await _userManager.GetUsersInRoleAsync(roleName);

                summaries.Add(new RoleSummaryDto
                {
                    Id = role.Id,
                    Name = roleName,
                    IsBuiltIn = AppRoles.IsBuiltIn(roleName),
                    PermissionCount = permissions.Count,
                    UserCount = users.Count,
                    Permissions = permissions
                });
            }

            return summaries
                .OrderByDescending(r => r.IsBuiltIn)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public async Task<RoleDetailsDto?> GetRoleByIdAsync(string roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                return null;
            }

            var role = await _roleManager.FindByIdAsync(roleId.Trim());
            if (role is null)
            {
                return null;
            }

            var roleName = role.Name ?? string.Empty;
            var permissions = await GetRolePermissionKeysAsync(role);
            IList<IdentityUser> users = string.IsNullOrWhiteSpace(roleName)
                ? []
                : await _userManager.GetUsersInRoleAsync(roleName);

            var userSummaries = new List<UserSummaryDto>(users.Count);
            foreach (var user in users.OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase))
            {
                var userRoles = (await _userManager.GetRolesAsync(user))
                    .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                userSummaries.Add(new UserSummaryDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    LockoutEnd = user.LockoutEnd,
                    AccessFailedCount = user.AccessFailedCount,
                    Roles = userRoles
                });
            }

            return new RoleDetailsDto
            {
                Id = role.Id,
                Name = roleName,
                IsBuiltIn = AppRoles.IsBuiltIn(roleName),
                Permissions = permissions,
                Users = userSummaries
            };
        }

        public async Task<OperationResult> CreateRoleAsync(RoleInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var roleName = input.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return OperationResult.Fail("El nombre del rol es obligatorio.");
            }

            if (AppRoles.IsBuiltIn(roleName))
            {
                return OperationResult.Fail($"El nombre '{roleName}' está reservado para un rol base del sistema.");
            }

            if (await _roleManager.FindByNameAsync(roleName) is not null)
            {
                return OperationResult.Fail($"Ya existe un rol registrado con el nombre '{roleName}'.");
            }

            if (!TryValidatePermissions(input.SelectedPermissions, out var validPermissions, out var invalidError))
            {
                return OperationResult.Fail(invalidError);
            }

            var role = new IdentityRole(roleName);
            var createResult = await _roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
            {
                return FailFromIdentityResult("No se pudo crear el rol.", createResult);
            }

            var syncResult = await SyncRoleClaimsAsync(role, validPermissions, refreshUserStamps: false);
            if (!syncResult.Succeeded)
            {
                return syncResult;
            }

            _logger.LogInformation(
                "Rol '{RoleName}' ({RoleId}) creado con {PermissionCount} permiso(s).",
                role.Name,
                role.Id,
                validPermissions.Count);

            return OperationResult.Ok($"Rol '{role.Name}' creado correctamente.", role.Id);
        }

        public async Task<OperationResult> UpdateRoleAsync(string roleId, RoleInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (string.IsNullOrWhiteSpace(roleId))
            {
                return OperationResult.Fail("El identificador del rol no es válido.");
            }

            var role = await _roleManager.FindByIdAsync(roleId.Trim());
            if (role is null)
            {
                return OperationResult.Fail("No se encontró el rol solicitado.");
            }

            var currentName = role.Name ?? string.Empty;
            var requestedName = input.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                return OperationResult.Fail("El nombre del rol es obligatorio.");
            }

            if (AppRoles.IsBuiltIn(currentName) &&
                !string.Equals(currentName, requestedName, StringComparison.Ordinal))
            {
                return OperationResult.Fail($"No se puede renombrar el rol base '{currentName}'.");
            }

            if (!AppRoles.IsBuiltIn(currentName) && AppRoles.IsBuiltIn(requestedName))
            {
                return OperationResult.Fail($"El nombre '{requestedName}' está reservado para un rol base del sistema.");
            }

            if (!TryValidatePermissionsForRole(currentName, input.SelectedPermissions, out var validPermissions, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            if (!string.Equals(currentName, requestedName, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await _roleManager.FindByNameAsync(requestedName);
                if (duplicate is not null && !string.Equals(duplicate.Id, role.Id, StringComparison.Ordinal))
                {
                    return OperationResult.Fail($"Ya existe otro rol con el nombre '{requestedName}'.");
                }
            }

            if (!string.Equals(currentName, requestedName, StringComparison.Ordinal))
            {
                role.Name = requestedName;
                var updateResult = await _roleManager.UpdateAsync(role);
                if (!updateResult.Succeeded)
                {
                    return FailFromIdentityResult("No se pudo actualizar el nombre del rol.", updateResult);
                }
            }

            var syncResult = await SyncRoleClaimsAsync(role, validPermissions, refreshUserStamps: true);
            if (!syncResult.Succeeded)
            {
                return syncResult;
            }

            _logger.LogInformation(
                "Rol '{RoleName}' ({RoleId}) actualizado con {PermissionCount} permiso(s).",
                role.Name,
                role.Id,
                validPermissions.Count);

            return OperationResult.Ok($"Rol '{role.Name}' actualizado correctamente.", role.Id);
        }

        public async Task<OperationResult> UpdateRolePermissionsAsync(RolePermissionsInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (string.IsNullOrWhiteSpace(input.RoleId))
            {
                return OperationResult.Fail("El identificador del rol es obligatorio.");
            }

            var role = await _roleManager.FindByIdAsync(input.RoleId.Trim());
            if (role is null)
            {
                return OperationResult.Fail("No se encontró el rol solicitado.");
            }

            var roleName = role.Name ?? string.Empty;
            if (!TryValidatePermissionsForRole(roleName, input.SelectedPermissions, out var validPermissions, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            var syncResult = await SyncRoleClaimsAsync(role, validPermissions, refreshUserStamps: true);
            if (!syncResult.Succeeded)
            {
                return syncResult;
            }

            _logger.LogInformation(
                "Permisos del rol '{RoleName}' ({RoleId}) actualizados ({PermissionCount} permisos).",
                role.Name,
                role.Id,
                validPermissions.Count);

            return OperationResult.Ok($"Permisos del rol '{role.Name}' actualizados correctamente.", role.Id);
        }

        public async Task<OperationResult> DeleteRoleAsync(string roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                return OperationResult.Fail("El identificador del rol no es válido.");
            }

            var role = await _roleManager.FindByIdAsync(roleId.Trim());
            if (role is null)
            {
                return OperationResult.Fail("No se encontró el rol solicitado.");
            }

            var roleName = role.Name ?? string.Empty;
            if (AppRoles.IsBuiltIn(roleName))
            {
                return OperationResult.Fail($"No se puede eliminar el rol base '{roleName}'.");
            }

            var assignedUsers = await _userManager.GetUsersInRoleAsync(roleName);
            if (assignedUsers.Count > 0)
            {
                return OperationResult.Fail(
                    $"No se puede eliminar el rol '{roleName}' porque tiene {assignedUsers.Count} usuario(s) asignado(s).");
            }

            var deleteResult = await _roleManager.DeleteAsync(role);
            if (!deleteResult.Succeeded)
            {
                return FailFromIdentityResult($"No se pudo eliminar el rol '{roleName}'.", deleteResult);
            }

            _logger.LogInformation("Rol '{RoleName}' ({RoleId}) eliminado.", roleName, role.Id);
            return OperationResult.Ok($"Rol '{roleName}' eliminado correctamente.", role.Id);
        }

        private async Task<IReadOnlyList<string>> GetRolePermissionKeysAsync(IdentityRole role)
        {
            var claims = await _roleManager.GetClaimsAsync(role);
            return claims
                .Where(c => c.Type == AppPermissions.ClaimType)
                .Select(c => AppPermissions.Find(c.Value)?.Key)
                .Where(key => key is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool TryValidatePermissionsForRole(
            string roleName,
            IEnumerable<string>? selectedPermissions,
            out IReadOnlyList<string> validPermissions,
            out string errorMessage)
        {
            if (!TryValidatePermissions(selectedPermissions, out validPermissions, out errorMessage))
            {
                return false;
            }

            if (string.Equals(roleName, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
                (!validPermissions.Contains(AppPermissions.RolesManage, StringComparer.OrdinalIgnoreCase) ||
                 !validPermissions.Contains(AppPermissions.UsersManage, StringComparer.OrdinalIgnoreCase)))
            {
                errorMessage = "El rol base 'Admin' debe conservar obligatoriamente los permisos de gestión de roles y usuarios.";
                return false;
            }

            return true;
        }

        private static bool TryValidatePermissions(
            IEnumerable<string>? selectedPermissions,
            out IReadOnlyList<string> validPermissions,
            out string errorMessage)
        {
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPermission in selectedPermissions ?? [])
            {
                if (string.IsNullOrWhiteSpace(rawPermission))
                {
                    continue;
                }

                var descriptor = AppPermissions.Find(rawPermission);
                if (descriptor is null)
                {
                    validPermissions = [];
                    errorMessage = $"El permiso '{rawPermission}' no es válido.";
                    return false;
                }

                if (seen.Add(descriptor.Key))
                {
                    normalized.Add(descriptor.Key);
                }
            }

            validPermissions = normalized;
            errorMessage = string.Empty;
            return true;
        }

        private async Task<OperationResult> SyncRoleClaimsAsync(
            IdentityRole role,
            IReadOnlyList<string> targetPermissions,
            bool refreshUserStamps)
        {
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var permissionClaims = existingClaims
                .Where(c => c.Type == AppPermissions.ClaimType)
                .ToList();

            var targetSet = targetPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var claim in permissionClaims)
            {
                if (targetSet.Contains(claim.Value))
                {
                    continue;
                }

                var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    return FailFromIdentityResult(
                        $"No se pudo remover el permiso '{claim.Value}' del rol '{role.Name}'.",
                        removeResult);
                }
            }

            var currentSet = permissionClaims
                .Where(c => AppPermissions.IsValid(c.Value))
                .Select(c => AppPermissions.Find(c.Value)!.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var permissionKey in targetPermissions)
            {
                if (currentSet.Contains(permissionKey))
                {
                    continue;
                }

                var addResult = await _roleManager.AddClaimAsync(
                    role,
                    new Claim(AppPermissions.ClaimType, permissionKey));

                if (!addResult.Succeeded)
                {
                    return FailFromIdentityResult(
                        $"No se pudo agregar el permiso '{permissionKey}' al rol '{role.Name}'.",
                        addResult);
                }
            }

            if (refreshUserStamps && !string.IsNullOrWhiteSpace(role.Name))
            {
                await RefreshSecurityStampsForRoleAsync(role.Name);
            }

            return OperationResult.Ok();
        }

        private async Task RefreshSecurityStampsForRoleAsync(string roleName)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in usersInRole)
            {
                var stampResult = await _userManager.UpdateSecurityStampAsync(user);
                if (!stampResult.Succeeded)
                {
                    _logger.LogWarning(
                        "No se pudo actualizar el SecurityStamp de '{UserName}' ({UserId}) tras cambiar el rol '{RoleName}'.",
                        user.UserName,
                        user.Id,
                        roleName);
                }
            }
        }

        private OperationResult FailFromIdentityResult(string message, IdentityResult result)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            _logger.LogWarning("{Message} Detalles: {Errors}", message, string.Join("; ", errors));
            return OperationResult.Fail(message, errors);
        }
    }
}
