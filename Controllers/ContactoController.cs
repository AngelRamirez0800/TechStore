using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Models;

namespace TechStore.Controllers
{
    [AllowAnonymous]
    public class ContactoController : Controller
    {
        private readonly ILogger<ContactoController> _logger;

        public ContactoController(ILogger<ContactoController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            _logger.LogInformation(
                "Acceso a formulario de contacto por usuario {User}.",
                User.Identity?.Name ?? "Anónimo");

            return View(new ContactoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(ContactoViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Formulario de contacto inválido enviado por usuario {User}. Errores: {ErrorCount}.",
                    User.Identity?.Name ?? "Anónimo",
                    ModelState.ErrorCount);

                return View(modelo);
            }

            _logger.LogInformation(
                "Mensaje de contacto enviado por {SenderName} (Asunto: {Subject}).",
                modelo.NombreCompleto,
                modelo.Asunto);

            TempData["SuccessMessage"] = $"¡Gracias, {modelo.NombreCompleto}! Tu mensaje fue recibido correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}