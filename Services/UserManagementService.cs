using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechStore.Constants;
using TechStore.DTOs.Admin;
using TechStore.Interfaces;

namespace TechStore.Services
{
    /// <summary>
    /// Servicio administrativo para consultar usuarios, asignar roles y gestionar bloqueos de cuenta.
    /// </summary>
    public sealed class UserManagementService : IUserManagementService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserManagementService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var summaries = new List<UserSummaryDto>(users.Count);
            foreach (var user in users)
            {
                summaries.Add(await BuildUserSummaryAsync(user));
            }

            return summaries;
        }

        public async Task<UserSummaryDto?> GetUserByIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(userId.Trim());
            if (user is null)
            {
                return null;
            }

            return await BuildUserSummaryAsync(user);
        }

        public async Task<UserRoleAssignmentInput?> GetUserRoleAssignmentAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(userId.Trim());
            if (user is null)
            {
                return null;
            }

            var userRoles = (await _userManager.GetRolesAsync(user))
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var availableRoles = (await _roleManager.Roles
                    .Select(r => r.Name)
                    .ToListAsync())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderByDescending(AppRoles.IsBuiltIn)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new UserRoleAssignmentInput
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                SelectedRoles = userRoles,
                AvailableRoles = availableRoles
            };
        }

        public async Task<OperationResult> UpdateUserRolesAsync(
            UserRoleAssignmentInput input,
            string? currentAdminUserId = null)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (string.IsNullOrWhiteSpace(input.UserId))
            {
                return OperationResult.Fail("El identificador del usuario es obligatorio.");
            }

            var user = await _userManager.FindByIdAsync(input.UserId.Trim());
            if (user is null)
            {
                return OperationResult.Fail("No se encontró el usuario solicitado.");
            }

            var roleResolution = await ResolveTargetRolesAsync(input.SelectedRoles);
            if (!roleResolution.Succeeded)
            {
                return OperationResult.Fail(roleResolution.ErrorMessage);
            }

            var targetRoles = roleResolution.RoleNames;
            var currentRoles = await _userManager.GetRolesAsync(user);

            var adminRemovalGuard = await ValidateAdminRoleRetentionAsync(
                user,
                currentRoles,
                targetRoles,
                currentAdminUserId);

            if (!adminRemovalGuard.Succeeded)
            {
                return adminRemovalGuard;
            }

            var rolesToAdd = targetRoles
                .Except(currentRoles, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var rolesToRemove = currentRoles
                .Except(targetRoles, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (rolesToAdd.Length > 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    return FailFromIdentityResult("No se pudieron asignar los nuevos roles al usuario.", addResult);
                }
            }

            if (rolesToRemove.Length > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    return FailFromIdentityResult("No se pudieron remover los roles anteriores del usuario.", removeResult);
                }
            }

            await RefreshSecurityStampAsync(user);

            _logger.LogInformation(
                "Roles del usuario '{UserName}' ({UserId}) actualizados a [{Roles}] por '{AdminUserId}'.",
                user.UserName,
                user.Id,
                string.Join(", ", targetRoles),
                currentAdminUserId ?? "Sistema");

            return OperationResult.Ok($"Roles de '{user.UserName}' actualizados correctamente.", user.Id);
        }

        public async Task<OperationResult> ToggleLockoutAsync(
            string userId,
            string? currentAdminUserId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return OperationResult.Fail("El identificador del usuario no es válido.");
            }

            var user = await _userManager.FindByIdAsync(userId.Trim());
            if (user is null)
            {
                return OperationResult.Fail("No se encontró el usuario solicitado.");
            }

            if (!string.IsNullOrWhiteSpace(currentAdminUserId) &&
                string.Equals(user.Id, currentAdminUserId, StringComparison.Ordinal))
            {
                return OperationResult.Fail("No puedes bloquear ni desbloquear tu propia cuenta.");
            }

            if (IsUserLockedOut(user))
            {
                return await UnlockUserAsync(user, currentAdminUserId);
            }

            return await LockUserAsync(user, currentAdminUserId);
        }

        private async Task<OperationResult> LockUserAsync(IdentityUser user, string? currentAdminUserId)
        {
            if (await _userManager.IsInRoleAsync(user, AppRoles.Admin) &&
                !await HasAnotherActiveAdminAsync(user.Id))
            {
                return OperationResult.Fail("No se puede bloquear al último administrador activo del sistema.");
            }

            if (!user.LockoutEnabled)
            {
                var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
                if (!enableResult.Succeeded)
                {
                    return FailFromIdentityResult("No se pudo habilitar el bloqueo para este usuario.", enableResult);
                }
            }

            var lockResult = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            if (!lockResult.Succeeded)
            {
                return FailFromIdentityResult("No se pudo bloquear la cuenta del usuario.", lockResult);
            }

            await RefreshSecurityStampAsync(user);

            _logger.LogInformation(
                "Usuario '{UserName}' ({UserId}) bloqueado por '{AdminUserId}'.",
                user.UserName,
                user.Id,
                currentAdminUserId ?? "Sistema");

            return OperationResult.Ok($"La cuenta de '{user.UserName}' ha sido bloqueada.", user.Id);
        }

        private async Task<OperationResult> UnlockUserAsync(IdentityUser user, string? currentAdminUserId)
        {
            var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!unlockResult.Succeeded)
            {
                return FailFromIdentityResult("No se pudo desbloquear la cuenta del usuario.", unlockResult);
            }

            var resetCountResult = await _userManager.ResetAccessFailedCountAsync(user);
            if (!resetCountResult.Succeeded)
            {
                _logger.LogWarning(
                    "No se pudo reiniciar AccessFailedCount para '{UserName}' ({UserId}).",
                    user.UserName,
                    user.Id);
            }

            await RefreshSecurityStampAsync(user);

            _logger.LogInformation(
                "Usuario '{UserName}' ({UserId}) desbloqueado por '{AdminUserId}'.",
                user.UserName,
                user.Id,
                currentAdminUserId ?? "Sistema");

            return OperationResult.Ok($"La cuenta de '{user.UserName}' ha sido desbloqueada.", user.Id);
        }

        private async Task<(bool Succeeded, IReadOnlyList<string> RoleNames, string ErrorMessage)> ResolveTargetRolesAsync(
            IEnumerable<string>? selectedRoles)
        {
            var resolved = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawRole in selectedRoles ?? [])
            {
                if (string.IsNullOrWhiteSpace(rawRole))
                {
                    continue;
                }

                var roleName = rawRole.Trim();
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role is null || string.IsNullOrWhiteSpace(role.Name))
                {
                    return (false, [], $"El rol '{roleName}' no existe.");
                }

                if (seen.Add(role.Name))
                {
                    resolved.Add(role.Name);
                }
            }

            return (true, resolved, string.Empty);
        }

        private async Task<OperationResult> ValidateAdminRoleRetentionAsync(
            IdentityUser targetUser,
            IList<string> currentRoles,
            IReadOnlyList<string> targetRoles,
            string? currentAdminUserId)
        {
            var hadAdminRole = currentRoles.Any(r => string.Equals(r, AppRoles.Admin, StringComparison.OrdinalIgnoreCase));
            var keepsAdminRole = targetRoles.Any(r => string.Equals(r, AppRoles.Admin, StringComparison.OrdinalIgnoreCase));

            if (!hadAdminRole || keepsAdminRole)
            {
                return OperationResult.Ok();
            }

            if (!string.IsNullOrWhiteSpace(currentAdminUserId) &&
                string.Equals(targetUser.Id, currentAdminUserId, StringComparison.Ordinal))
            {
                return OperationResult.Fail("No puedes quitarte el rol 'Admin' de tu propia cuenta.");
            }

            if (!await HasAnotherActiveAdminAsync(targetUser.Id))
            {
                return OperationResult.Fail(
                    "Debe permanecer al menos un administrador activo en el sistema. No se puede quitar el rol 'Admin' al último administrador activo.");
            }

            return OperationResult.Ok();
        }

        private async Task<bool> HasAnotherActiveAdminAsync(string excludedUserId)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            return adminUsers.Any(admin =>
                !string.Equals(admin.Id, excludedUserId, StringComparison.Ordinal) &&
                !IsUserLockedOut(admin));
        }

        private async Task<UserSummaryDto> BuildUserSummaryAsync(IdentityUser user)
        {
            var roles = (await _userManager.GetRolesAsync(user))
                .OrderByDescending(AppRoles.IsBuiltIn)
                .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new UserSummaryDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                AccessFailedCount = user.AccessFailedCount,
                Roles = roles
            };
        }

        private async Task RefreshSecurityStampAsync(IdentityUser user)
        {
            var stampResult = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                _logger.LogWarning(
                    "No se pudo actualizar el SecurityStamp del usuario '{UserName}' ({UserId}).",
                    user.UserName,
                    user.Id);
            }
        }

        private OperationResult FailFromIdentityResult(string message, IdentityResult result)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            _logger.LogWarning("{Message} Detalles: {Errors}", message, string.Join("; ", errors));
            return OperationResult.Fail(message, errors);
        }

        private static bool IsUserLockedOut(IdentityUser user) =>
            user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
    }
}
