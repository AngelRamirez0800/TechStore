using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TechStore.Constants;
using TechStore.Interfaces;

namespace TechStore.Data.Seeders
{
    /// <summary>
    /// Inicializa de forma idempotente los roles, permisos y usuarios base del sistema.
    /// </summary>
    public sealed class IdentitySeeder : IIdentitySeeder
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<IdentitySeeder> _logger;

        public IdentitySeeder(
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager,
            ILogger<IdentitySeeder> logger)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAsync()
        {
            await EnsureRoleAsync(
                AppRoles.Admin,
                AppPermissions.AllKeys,
                syncMissingPermissions: true);

            await EnsureRoleAsync(
                AppRoles.Client,
                AppPermissions.DefaultClientPermissions,
                syncMissingPermissions: false);

            await EnsureUserAsync("Angel", "angel@techstore.com", "Angel-247", AppRoles.Client);
            await EnsureUserAsync("Daniel", "daniel@techstore.com", "Daniel-0800", AppRoles.Admin);
        }

        private async Task EnsureRoleAsync(
            string roleName,
            IReadOnlyList<string> defaultPermissions,
            bool syncMissingPermissions)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            var isNewRole = false;

            if (role is null)
            {
                role = new IdentityRole(roleName);
                var createResult = await _roleManager.CreateAsync(role);
                if (!createResult.Succeeded)
                {
                    LogIdentityErrors($"No se pudo crear el rol '{roleName}'", createResult);
                    return;
                }

                isNewRole = true;
                _logger.LogInformation("Rol base '{RoleName}' creado durante el seed inicial.", roleName);
            }

            // Solo Admin sincroniza permisos faltantes en cada arranque;
            // Client conserva los cambios personalizados hechos luego desde administración.
            if (!isNewRole && !syncMissingPermissions)
            {
                return;
            }

            await EnsureRolePermissionsAsync(role, defaultPermissions);
        }

        private async Task EnsureRolePermissionsAsync(IdentityRole role, IReadOnlyList<string> requiredPermissions)
        {
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingKeys = existingClaims
                .Where(c => c.Type == AppPermissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var addedCount = 0;
            foreach (var permissionKey in requiredPermissions)
            {
                if (existingKeys.Contains(permissionKey))
                {
                    continue;
                }

                var claimResult = await _roleManager.AddClaimAsync(
                    role,
                    new Claim(AppPermissions.ClaimType, permissionKey));

                if (!claimResult.Succeeded)
                {
                    LogIdentityErrors($"No se pudo asignar el permiso '{permissionKey}' al rol '{role.Name}'", claimResult);
                    continue;
                }

                addedCount++;
            }

            if (addedCount > 0)
            {
                _logger.LogInformation(
                    "Se asignaron {PermissionCount} permiso(s) al rol '{RoleName}' durante el seed.",
                    addedCount,
                    role.Name);
            }
        }

        private async Task EnsureUserAsync(string userName, string email, string password, string roleName)
        {
            var user = await _userManager.FindByNameAsync(userName)
                ?? await _userManager.FindByEmailAsync(email);

            var isNewUser = false;
            if (user is null)
            {
                user = new IdentityUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };

                var createResult = await _userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    LogIdentityErrors($"No se pudo crear el usuario '{userName}' ({email})", createResult);
                    return;
                }

                isNewUser = true;
                _logger.LogInformation("Usuario '{UserName}' ({Email}) creado durante el seed inicial.", userName, email);
            }

            await EnsureInitialUserRoleAsync(user, roleName, isNewUser);
        }

        private async Task EnsureInitialUserRoleAsync(IdentityUser user, string roleName, bool isNewUser)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogWarning(
                    "No se pudo asignar el rol '{RoleName}' a '{UserName}' porque el rol no existe.",
                    roleName,
                    user.UserName);
                return;
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var needsAdminFallback = string.Equals(roleName, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                && !currentRoles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase)
                && (await _userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count == 0;

            if (!isNewUser && currentRoles.Count > 0 && !needsAdminFallback)
            {
                return;
            }

            if (currentRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                LogIdentityErrors($"No se pudo asignar el rol '{roleName}' al usuario '{user.UserName}'", roleResult);
                return;
            }

            _logger.LogInformation("Rol '{RoleName}' asignado al usuario '{UserName}'.", roleName, user.UserName);
        }

        private void LogIdentityErrors(string contextMessage, IdentityResult result)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogError("{ContextMessage}. Errores: {Errors}", contextMessage, errors);
        }
    }
}
