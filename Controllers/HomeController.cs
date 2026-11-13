using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Interfaces;
using TechStore.Models;

namespace TechStore.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IProductQueries _productQueries;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IProductQueries productQueries,
            ILogger<HomeController> logger)
        {
            _productQueries = productQueries;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _productQueries.GetAllActiveAsync();

            var featuredProducts = products
                .Where(product => product.Featured)
                .ToList();

            _logger.LogInformation(
                "Acceso a inicio por usuario {User}. Productos destacados mostrados: {FeaturedCount}.",
                User.Identity?.Name ?? "Anónimo",
                featuredProducts.Count);

            return View(featuredProducts);
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            _logger.LogWarning("Renderizando vista de error genérica en Home/Error. RequestId: {RequestId}.", requestId);

            return View(new ErrorViewModel
            {
                RequestId = requestId
            });
        }
    }
}