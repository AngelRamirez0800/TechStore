using Microsoft.AspNetCore.Mvc;
using TechStore.Interfaces;

namespace TechStore.ViewComponents
{
    /// <summary>
    /// ViewComponent de navegación rápida que lista las categorías activas del catálogo.
    /// </summary>
    public sealed class CategoryMenuViewComponent : ViewComponent
    {
        private readonly ICategoryQueries _categoryQueries;

        public CategoryMenuViewComponent(ICategoryQueries categoryQueries)
        {
            _categoryQueries = categoryQueries;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var allCategories = await _categoryQueries.GetAllAsync();
            var activeCategories = allCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();

            return View(activeCategories);
        }
    }
}
