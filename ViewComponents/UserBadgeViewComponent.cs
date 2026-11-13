using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TechStore.Security.Authorization;

namespace TechStore.ViewComponents
{
    /// <summary>
    /// ViewComponent que muestra el usuario autenticado y sus insignias de rol en la barra de navegación.
    /// </summary>
    public sealed class UserBadgeViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var userName = HttpContext.User.Identity.Name ?? "Usuario";
            var roles = HttpContext.User.Claims
                .Where(c => c.Type == ClaimTypes.Role || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => string.Equals(r, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) ? 0
                    : string.Equals(r, AppRoles.Client, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return View(new UserBadgeViewModel(userName, roles));
        }
    }

    public sealed record UserBadgeViewModel(string UserName, IReadOnlyList<string> Roles);
}
