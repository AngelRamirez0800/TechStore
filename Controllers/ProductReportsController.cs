
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Interfaces;
using TechStore.Security.Authorization;

namespace TechStore.Controllers
{
    [Authorize]
    [Route("ProductReports")]
    public class ProductReportsController : Controller
    {
        private readonly IProductQueries _productQueries;
        private readonly ICategoryQueries _categoryQueries;
        private readonly IProductReportService _reportService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<ProductReportsController> _logger;

        public ProductReportsController(
            IProductQueries productQueries,
            ICategoryQueries categoryQueries,
            IProductReportService reportService,
            IAuthorizationService authorizationService,
            ILogger<ProductReportsController> logger)
        {
            _productQueries = productQueries;
            _categoryQueries = categoryQueries;
            _reportService = reportService;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        [HttpGet("Pdf")]
        [Authorize(Policy = AppPermissions.ProductsRead)]
        public async Task<IActionResult> ExportPdf(
            List<Guid>? categoryIds = null,
            Guid? categoryId = null,
            List<string>? statuses = null)
        {
            try
            {
                var products = await GetFilteredProductsAsync(
                    categoryIds, categoryId, statuses);

                var pdf = _reportService.GenerateProductsPdf(products);

                var fileName = $"TechStore_Productos_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                _logger.LogInformation(
                    "Reporte PDF generado por {User}. Productos: {Count}.",
                    User.Identity?.Name,
                    products.Count);

                return File(pdf, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el reporte PDF.");

                TempData["ErrorMessage"] =
                    "No fue posible generar el reporte PDF.";

                return RedirectToAction("Index", "Products");
            }
        }

        [HttpGet("Excel")]
        [Authorize(Policy = AppPermissions.ProductsRead)]
        public async Task<IActionResult> ExportExcel(
            List<Guid>? categoryIds = null,
            Guid? categoryId = null,
            List<string>? statuses = null)
        {
            try
            {
                var products = await GetFilteredProductsAsync(
                    categoryIds, categoryId, statuses);

                var excel = _reportService.GenerateProductsExcel(products);

                var fileName = $"TechStore_Productos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                _logger.LogInformation(
                    "Reporte Excel generado por {User}. Productos: {Count}.",
                    User.Identity?.Name,
                    products.Count);

                return File(
                    excel,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el reporte Excel.");

                TempData["ErrorMessage"] =
                    "No fue posible generar el reporte Excel.";

                return RedirectToAction("Index", "Products");
            }
        }

        private async Task<List<TechStore.Models.Product>> GetFilteredProductsAsync(
            List<Guid>? categoryIds,
            Guid? categoryId,
            List<string>? statuses)
        {
            var canManageLifecycle = (await _authorizationService.AuthorizeAsync(
                User,
                AppPermissions.ProductsLifecycle)).Succeeded;

            var canManageCategoryLifecycle = (await _authorizationService.AuthorizeAsync(
                User,
                AppPermissions.CategoriesLifecycle)).Succeeded;

            // Obtener productos según los permisos del usuario.
            var products = (canManageLifecycle
                    ? await _productQueries.GetAllAsync()
                    : await _productQueries.GetAllActiveAsync())
                .ToList();

            // Normalizar categorías seleccionadas.
            var selectedCategoryIds = (categoryIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .ToHashSet();

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                selectedCategoryIds.Add(categoryId.Value);
            }

            // Filtrar por categorías.
            if (selectedCategoryIds.Count > 0)
            {
                products = products
                    .Where(product => product.Categories.Any(category =>
                        selectedCategoryIds.Contains(category.Id) &&
                        (canManageCategoryLifecycle || category.IsActive)))
                    .ToList();
            }

            // Los usuarios sin permiso de ciclo de vida solo ven activos.
            if (!canManageLifecycle)
            {
                return products.Where(product => product.IsActive).ToList();
            }

            // Filtrar por estado cuando el usuario tiene permiso.
            var selectedStatuses = (statuses ?? new List<string>())
                .Where(status =>
                    string.Equals(status, "active", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
                .Select(status => status.ToLowerInvariant())
                .ToHashSet();

            if (selectedStatuses.Count > 0)
            {
                products = products
                    .Where(product =>
                        (selectedStatuses.Contains("active") && product.IsActive) ||
                        (selectedStatuses.Contains("inactive") && !product.IsActive))
                    .ToList();
            }

            return products;
        }
    }
}
