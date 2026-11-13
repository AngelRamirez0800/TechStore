using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace TechStore.Security.Authorization
{
    /// <summary>
    /// Extensiones para registrar las políticas de autorización basadas en roles y permisos de TechStore.
    /// </summary>
    public static class AuthorizationExtensions
    {
        /// <summary>
        /// Registra dinámicamente una política de autorización por cada permiso definido en <see cref="AppPermissions.AllKeys"/>.
        /// </summary>
        public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddAuthorization(options =>
            {
                foreach (var permissionKey in AppPermissions.AllKeys)
                {
                    options.AddPolicy(permissionKey, policy => ConfigurePermissionPolicy(policy, permissionKey));
                }
            });

            return services;
        }

        private static void ConfigurePermissionPolicy(AuthorizationPolicyBuilder policy, string permissionKey)
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => HasPermission(context.User, permissionKey));
        }

        private static bool HasPermission(ClaimsPrincipal user, string permissionKey)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return user.IsInRole(AppRoles.Admin) ||
                   user.HasClaim(AppPermissions.ClaimType, permissionKey);
        }
    }
}
