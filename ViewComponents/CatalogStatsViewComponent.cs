using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Constants;
using TechStore.Interfaces;

namespace TechStore.ViewComponents
{
    /// <summary>
    /// ViewComponent que muestra tarjetas KPI con estadísticas del catálogo según los permisos del usuario.
    /// </summary>
    public sealed class CatalogStatsViewComponent : ViewComponent
    {
        private readonly IProductQueries _productQueries;
        private readonly ICategoryQueries _categoryQueries;
        private readonly IAuthorizationService _authorizationService;

        public CatalogStatsViewComponent(
            IProductQueries productQueries,
            ICategoryQueries categoryQueries,
            IAuthorizationService authorizationService)
        {
            _productQueries = productQueries;
            _categoryQueries = categoryQueries;
            _authorizationService = authorizationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var canViewLifecycle = (await _authorizationService.AuthorizeAsync(
                HttpContext.User,
                AppPermissions.ProductsLifecycle)).Succeeded;

            var products = canViewLifecycle
                ? await _productQueries.GetAllAsync()
                : await _productQueries.GetAllActiveAsync();

            var categories = canViewLifecycle
                ? await _categoryQueries.GetAllAsync()
                : await _categoryQueries.GetAllActiveAsync();

            var activeProducts = products.Where(p => p.IsActive).ToList();

            var model = new CatalogStatsViewModel(
                ActiveProductsCount: activeProducts.Count,
                ActiveCategoriesCount: categories.Count(c => c.IsActive),
                TotalStock: activeProducts.Sum(p => p.Stock),
                InactiveProductsCount: canViewLifecycle ? products.Count(p => !p.IsActive) : 0,
                ShowInactiveCount: canViewLifecycle);

            return View(model);
        }
    }

    public sealed record CatalogStatsViewModel(
        int ActiveProductsCount,
        int ActiveCategoriesCount,
        int TotalStock,
        int InactiveProductsCount,
        bool ShowInactiveCount);
}
