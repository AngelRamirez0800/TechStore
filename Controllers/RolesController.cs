using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Constants;
using TechStore.DTOs.Admin;
using TechStore.Interfaces;

namespace TechStore.Controllers
{
    /// <summary>
    /// Controlador administrativo para la gestión de roles, permisos por Role Claims y usuarios.
    /// </summary>
    [Authorize]
    public class RolesController : Controller
    {
        private readonly IRoleManagementService _roleManagementService;
        private readonly IUserManagementService _userManagementService;
        private readonly ILogger<RolesController> _logger;

        public RolesController(
            IRoleManagementService roleManagementService,
            IUserManagementService userManagementService,
            ILogger<RolesController> logger)
        {
            _roleManagementService = roleManagementService ?? throw new ArgumentNullException(nameof(roleManagementService));
            _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManagementService.GetAllRolesAsync();
            return View(roles);
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var role = await _roleManagementService.GetRoleByIdAsync(id);
            if (role is null)
            {
                return NotFound();
            }

            return View(role);
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public IActionResult Create()
        {
            return View(new RoleInput());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Create(RoleInput input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var result = await _roleManagementService.CreateRoleAsync(input);
            if (!result.Succeeded)
            {
                AddOperationErrorsToModelState(result);
                _logger.LogWarning(
                    "Fallo al crear rol '{RoleName}' por '{Actor}': {Message}",
                    input.Name,
                    CurrentActorName,
                    result.Message);
                return View(input);
            }

            _logger.LogInformation(
                "Rol '{RoleName}' creado por '{Actor}'.",
                input.Name,
                CurrentActorName);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var role = await _roleManagementService.GetRoleByIdAsync(id);
            if (role is null)
            {
                return NotFound();
            }

            var model = new RoleInput
            {
                Id = role.Id,
                Name = role.Name,
                IsBuiltIn = role.IsBuiltIn,
                SelectedPermissions = role.Permissions.ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Edit(string id, RoleInput input)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            input.Id = id;

            if (!ModelState.IsValid)
            {
                var existing = await _roleManagementService.GetRoleByIdAsync(id);
                if (existing is null)
                {
                    return NotFound();
                }

                input.IsBuiltIn = existing.IsBuiltIn;
                return View(input);
            }

            var result = await _roleManagementService.UpdateRoleAsync(id, input);
            if (!result.Succeeded)
            {
                var existing = await _roleManagementService.GetRoleByIdAsync(id);
                if (existing is null)
                {
                    return NotFound();
                }

                input.IsBuiltIn = existing.IsBuiltIn;
                AddOperationErrorsToModelState(result);
                _logger.LogWarning(
                    "Fallo al actualizar rol '{RoleId}' por '{Actor}': {Message}",
                    id,
                    CurrentActorName,
                    result.Message);
                return View(input);
            }

            _logger.LogInformation(
                "Rol '{RoleName}' ({RoleId}) actualizado por '{Actor}'.",
                input.Name,
                id,
                CurrentActorName);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Permissions(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var role = await _roleManagementService.GetRoleByIdAsync(id);
            if (role is null)
            {
                return NotFound();
            }

            var model = new RolePermissionsInput
            {
                RoleId = role.Id,
                RoleName = role.Name,
                SelectedPermissions = role.Permissions.ToList()
            };

            ViewBag.IsBuiltIn = role.IsBuiltIn;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Permissions(string id, RolePermissionsInput input)
        {
            if (string.IsNullOrWhiteSpace(input.RoleId) && !string.IsNullOrWhiteSpace(id))
            {
                input.RoleId = id;
            }

            // RoleName es solo informativo en la vista y se rehidrata desde el servidor.
            ModelState.Remove(nameof(RolePermissionsInput.RoleName));

            if (string.IsNullOrWhiteSpace(input.RoleId))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return await ReturnPermissionsViewWithMetadataAsync(input);
            }

            var result = await _roleManagementService.UpdateRolePermissionsAsync(input);
            if (!result.Succeeded)
            {
                AddOperationErrorsToModelState(result);
                _logger.LogWarning(
                    "Fallo al actualizar permisos del rol '{RoleId}' por '{Actor}': {Message}",
                    input.RoleId,
                    CurrentActorName,
                    result.Message);
                return await ReturnPermissionsViewWithMetadataAsync(input);
            }

            _logger.LogInformation(
                "Permisos del rol '{RoleId}' actualizados por '{Actor}'.",
                input.RoleId,
                CurrentActorName);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.RolesManage)]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "El identificador del rol no es válido.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManagementService.DeleteRoleAsync(id);
            if (result.Succeeded)
            {
                _logger.LogInformation("Rol '{RoleId}' eliminado por '{Actor}'.", id, CurrentActorName);
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                _logger.LogWarning(
                    "No se pudo eliminar rol '{RoleId}' solicitado por '{Actor}': {Message}",
                    id,
                    CurrentActorName,
                    result.Message);
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.UsersManage)]
        public async Task<IActionResult> Users()
        {
            var users = await _userManagementService.GetAllUsersAsync();
            ViewBag.CurrentUserId = CurrentUserId;
            return View(users);
        }

        [HttpGet]
        [Authorize(Policy = AppPermissions.UsersManage)]
        public async Task<IActionResult> AssignRoles(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var model = await _userManagementService.GetUserRoleAssignmentAsync(id);
            if (model is null)
            {
                return NotFound();
            }

            ViewBag.CurrentUserId = CurrentUserId;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.UsersManage)]
        public async Task<IActionResult> AssignRoles(string id, UserRoleAssignmentInput input)
        {
            if (string.IsNullOrWhiteSpace(input.UserId) && !string.IsNullOrWhiteSpace(id))
            {
                input.UserId = id;
            }

            // Campos informativos hidratados desde el servidor.
            ModelState.Remove(nameof(UserRoleAssignmentInput.UserName));
            ModelState.Remove(nameof(UserRoleAssignmentInput.Email));

            if (string.IsNullOrWhiteSpace(input.UserId))
            {
                return NotFound();
            }

            var currentAdminUserId = CurrentUserId;

            if (!ModelState.IsValid)
            {
                return await ReturnAssignRolesViewWithMetadataAsync(input, currentAdminUserId);
            }

            var result = await _userManagementService.UpdateUserRolesAsync(input, currentAdminUserId);
            if (!result.Succeeded)
            {
                AddOperationErrorsToModelState(result);
                _logger.LogWarning(
                    "Fallo al asignar roles al usuario '{UserId}' por '{Actor}': {Message}",
                    input.UserId,
                    CurrentActorName,
                    result.Message);
                return await ReturnAssignRolesViewWithMetadataAsync(input, currentAdminUserId);
            }

            _logger.LogInformation(
                "Roles actualizados para el usuario '{UserId}' por '{Actor}'.",
                input.UserId,
                CurrentActorName);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.UsersManage)]
        public async Task<IActionResult> ToggleLockout(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "El identificador del usuario no es válido.";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManagementService.ToggleLockoutAsync(id, CurrentUserId);
            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Estado de bloqueo actualizado para usuario '{UserId}' por '{Actor}'.",
                    id,
                    CurrentActorName);
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                _logger.LogWarning(
                    "Fallo al cambiar bloqueo para usuario '{UserId}' por '{Actor}': {Message}",
                    id,
                    CurrentActorName,
                    result.Message);
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Users));
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string CurrentActorName => User.Identity?.Name ?? CurrentUserId ?? "Desconocido";

        private async Task<IActionResult> ReturnPermissionsViewWithMetadataAsync(RolePermissionsInput input)
        {
            var existing = await _roleManagementService.GetRoleByIdAsync(input.RoleId);
            if (existing is null)
            {
                return NotFound();
            }

            input.RoleName = existing.Name;
            ViewBag.IsBuiltIn = existing.IsBuiltIn;
            return View(input);
        }

        private async Task<IActionResult> ReturnAssignRolesViewWithMetadataAsync(
            UserRoleAssignmentInput input,
            string? currentAdminUserId)
        {
            var existing = await _userManagementService.GetUserRoleAssignmentAsync(input.UserId);
            if (existing is null)
            {
                return NotFound();
            }

            input.UserName = existing.UserName;
            input.Email = existing.Email;
            input.AvailableRoles = existing.AvailableRoles;
            ViewBag.CurrentUserId = currentAdminUserId;
            return View(input);
        }

        private void AddOperationErrorsToModelState(OperationResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                ModelState.AddModelError(string.Empty, result.Message);
            }

            foreach (var error in result.Errors)
            {
                if (!string.Equals(error, result.Message, StringComparison.Ordinal))
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
        }
    }
}
