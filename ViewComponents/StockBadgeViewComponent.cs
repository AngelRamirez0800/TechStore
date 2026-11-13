using Microsoft.AspNetCore.Mvc;

namespace TechStore.ViewComponents
{
    /// <summary>
    /// ViewComponent reutilizable que renderiza una insignia de disponibilidad según el stock y estado del producto.
    /// </summary>
    public sealed class StockBadgeViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(int stock, bool isActive = true)
        {
            return View(new StockBadgeViewModel(stock, isActive));
        }
    }

    public sealed record StockBadgeViewModel(int Stock, bool IsActive);
}
