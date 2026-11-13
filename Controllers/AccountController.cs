using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TechStore.Security.Authorization;
using TechStore.DTOs.Auth;

namespace TechStore.Controllers
{
    /// <summary>
    /// Controlador MVC encargado de autenticación, registro, cierre de sesión y acceso denegado.
    /// </summary>
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            return View(new LoginInput
            {
                ReturnUrl = NormalizeLocalReturnUrl(returnUrl)
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginInput model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var identifier = model.Identifier.Trim();
            var user = await FindUserByIdentifierAsync(identifier);

            if (user is null)
            {
                _logger.LogWarning(
                    "Intento de inicio de sesión fallido: no se encontró cuenta para el identificador {Identifier}.",
                    identifier);

                ModelState.AddModelError(string.Empty, "Usuario, correo electrónico o contraseña incorrectos.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Inicio de sesión exitoso para el usuario {UserName} ({UserId}).",
                    user.UserName,
                    user.Id);

                return RedirectToLocal(model.ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning(
                    "Cuenta bloqueada durante el intento de inicio de sesión para el usuario {UserName} ({UserId}).",
                    user.UserName,
                    user.Id);

                ModelState.AddModelError(
                    string.Empty,
                    "Tu cuenta está temporalmente bloqueada por demasiados intentos fallidos o por un administrador.");
                return View(model);
            }

            if (result.IsNotAllowed)
            {
                _logger.LogWarning(
                    "Inicio de sesión no permitido para el usuario {UserName} ({UserId}).",
                    user.UserName,
                    user.Id);

                ModelState.AddModelError(
                    string.Empty,
                    "Tu cuenta no tiene permitido iniciar sesión en este momento. Contacta al administrador.");
                return View(model);
            }

            _logger.LogWarning(
                "Credenciales inválidas en el inicio de sesión para el usuario {UserName} ({UserId}).",
                user.UserName,
                user.Id);

            ModelState.AddModelError(string.Empty, "Usuario, correo electrónico o contraseña incorrectos.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            return View(new RegisterInput
            {
                ReturnUrl = NormalizeLocalReturnUrl(returnUrl)
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterInput model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userName = model.UserName.Trim();
            var email = model.Email.Trim();

            if (await _userManager.FindByNameAsync(userName) is not null)
            {
                ModelState.AddModelError(nameof(model.UserName), "Este nombre de usuario ya está en uso.");
            }

            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                ModelState.AddModelError(nameof(model.Email), "Este correo electrónico ya está registrado.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new IdentityUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "Falló el registro del usuario {UserName} ({Email}): {Errors}",
                    userName,
                    email,
                    string.Join("; ", createResult.Errors.Select(error => error.Code)));

                AddIdentityErrorsToModelState(createResult);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Client);
            if (!roleResult.Succeeded)
            {
                _logger.LogError(
                    "No se pudo asignar el rol {Role} al usuario {UserName} ({UserId}). Se revertirá la creación.",
                    AppRoles.Client,
                    user.UserName,
                    user.Id);

                var rollbackResult = await _userManager.DeleteAsync(user);
                if (!rollbackResult.Succeeded)
                {
                    _logger.LogError(
                        "Error al revertir la creación del usuario {UserName} ({UserId}) después del fallo de rol.",
                        user.UserName,
                        user.Id);
                }

                ModelState.AddModelError(
                    string.Empty,
                    "No se pudo completar el registro de la cuenta en este momento. Inténtalo nuevamente.");
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation(
                "Nuevo usuario registrado e iniciado sesión: {UserName} ({UserId}) con rol {Role}.",
                user.UserName,
                user.Id,
                AppRoles.Client);

            TempData["SuccessMessage"] = $"¡Bienvenido a TechStore, {user.UserName}! Tu cuenta fue creada correctamente.";
            return RedirectToLocal(model.ReturnUrl);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name ?? "Desconocido";
            await _signInManager.SignOutAsync();

            _logger.LogInformation("El usuario {UserName} cerró sesión correctamente.", userName);
            TempData["InfoMessage"] = "Has cerrado sesión correctamente.";

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            var userName = User.Identity?.IsAuthenticated == true
                ? (User.Identity.Name ?? "Autenticado")
                : "Anónimo";

            var safeReturnUrl = NormalizeLocalReturnUrl(returnUrl);

            _logger.LogWarning(
                "Acceso denegado (403) para el usuario {UserName} al intentar acceder a {ReturnUrl}.",
                userName,
                string.IsNullOrWhiteSpace(returnUrl) ? "(no especificada)" : returnUrl);

            Response.StatusCode = StatusCodes.Status403Forbidden;
            ViewBag.ReturnUrl = safeReturnUrl;

            return View();
        }

        private async Task<IdentityUser?> FindUserByIdentifierAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            return await _userManager.FindByNameAsync(identifier)
                ?? await _userManager.FindByEmailAsync(identifier);
        }

        private string? NormalizeLocalReturnUrl(string? returnUrl) =>
            !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null;

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            var safeUrl = NormalizeLocalReturnUrl(returnUrl);
            if (safeUrl is not null)
            {
                return LocalRedirect(safeUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private void AddIdentityErrorsToModelState(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                switch (error.Code)
                {
                    case "DuplicateUserName":
                        ModelState.AddModelError(nameof(RegisterInput.UserName), "Este nombre de usuario ya está en uso.");
                        break;
                    case "InvalidUserName":
                        ModelState.AddModelError(
                            nameof(RegisterInput.UserName),
                            "El nombre de usuario contiene caracteres no permitidos.");
                        break;
                    case "DuplicateEmail":
                        ModelState.AddModelError(nameof(RegisterInput.Email), "Este correo electrónico ya está registrado.");
                        break;
                    case "InvalidEmail":
                        ModelState.AddModelError(nameof(RegisterInput.Email), "El correo electrónico ingresado no es válido.");
                        break;
                    case "PasswordTooShort":
                        ModelState.AddModelError(nameof(RegisterInput.Password), "La contraseña debe tener al menos 6 caracteres.");
                        break;
                    case "PasswordRequiresNonAlphanumeric":
                        ModelState.AddModelError(
                            nameof(RegisterInput.Password),
                            "La contraseña debe incluir al menos un carácter especial.");
                        break;
                    case "PasswordRequiresDigit":
                        ModelState.AddModelError(
                            nameof(RegisterInput.Password),
                            "La contraseña debe incluir al menos un número.");
                        break;
                    case "PasswordRequiresLower":
                        ModelState.AddModelError(
                            nameof(RegisterInput.Password),
                            "La contraseña debe incluir al menos una letra minúscula.");
                        break;
                    case "PasswordRequiresUpper":
                        ModelState.AddModelError(
                            nameof(RegisterInput.Password),
                            "La contraseña debe incluir al menos una letra mayúscula.");
                        break;
                    case "PasswordRequiresUniqueChars":
                        ModelState.AddModelError(
                            nameof(RegisterInput.Password),
                            "La contraseña debe incluir más caracteres diferentes.");
                        break;
                    default:
                        ModelState.AddModelError(string.Empty, error.Description);
                        break;
                }
            }
        }
    }
}
