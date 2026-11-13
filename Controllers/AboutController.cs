using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TechStore.Controllers
{
    [AllowAnonymous]
    public class AboutController : Controller
    {
        private readonly ILogger<AboutController> _logger;

        public AboutController(ILogger<AboutController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation(
                "Acceso a página Acerca de nosotros por usuario {User}.",
                User.Identity?.Name ?? "Anónimo");

            return View();
        }
    }
}
