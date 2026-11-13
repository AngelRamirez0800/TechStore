using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Constants;
using TechStore.DTOs;
using TechStore.Helpers;
using TechStore.Interfaces;

namespace TechStore.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly ICategoryQueries _categoryQueries;
        private readonly ICategoryApplicationService _categoryApplication;
        private readonly ICategoryLifecycle _categoryLifecycle;
        private readonly IProductQueries _productQueries;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(
            ICategoryQueries categoryQueries,
            ICategoryApplicationService categoryApplication,
            ICategoryLifecycle categoryLifecycle,
            IProductQueries productQueries,
            IAuthorizationService authorizationService,
            ILogger<CategoryController> logger)
        {
            _categoryQueries = categoryQueries;
            _categoryApplication = categoryApplication;
            _categoryLifecycle = categoryLifecycle;
            _productQueries = productQueries;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        [Authorize(Policy = AppPermissions.CategoriesRead)]
        public async Task<IActionResult> Index(
            List<Guid>? productIds = null,
            List<string>? statuses = null)
        {
            var canManageLifecycle = await CanManageCategoriesLifecycleAsync();
            var canViewInactiveProducts = await CanManageProductsLifecycleAsync();
            var selectedProductIds = CatalogFilterHelper.NormalizeGuids(productIds);
            var selectedStatuses = CatalogFilterHelper.NormalizeStatuses(statuses, canManageLifecycle);

            var categories = canManageLifecycle
                ? await _categoryQueries.GetAllAsync()
                : await _categoryQueries.GetAllActiveAsync();

            var filterProducts = (canViewInactiveProducts
                ? await _productQueries.GetAllAsync()
                : await _productQueries.GetAllActiveAsync())
                .OrderBy(product => product.Name)
                .ToList();

            var filteredCategories = categories.AsEnumerable();

            if (selectedProductIds.Count > 0)
            {
                filteredCategories = filteredCategories.Where(category =>
                    category.Products.Any(product =>
                        selectedProductIds.Contains(product.Id) &&
                        (canViewInactiveProducts || product.IsActive)));
            }

            filteredCategories = CatalogFilterHelper.FilterByStatus(
                filteredCategories,
                selectedStatuses,
                category => category.IsActive);

            var categoryList = filteredCategories.ToList();

            ViewBag.CanManageLifecycle = canManageLifecycle;
            ViewBag.CanViewInactiveProducts = canViewInactiveProducts;
            ViewBag.FilterProducts = filterProducts;
            ViewBag.SelectedProductIds = selectedProductIds;
            ViewBag.SelectedStatuses = selectedStatuses;

            _logger.LogInformation(
                "Usuario {User} consultó categorías (Total: {Count}, ProductIds: {ProductIds}, Statuses: {Statuses}).",
                GetCurrentUserName(),
                categoryList.Count,
                string.Join(",", selectedProductIds),
                string.Join(",", selectedStatuses));

            return View(categoryList);
        }

        [Authorize(Policy = AppPermissions.CategoriesRead)]
        public async Task<IActionResult> Details(Guid id)
        {
            var category = await _categoryQueries.GetByIdWithProductsAsync(id);
            if (category == null)
            {
                _logger.LogWarning(
                    "Categoría {CategoryId} no encontrada al consultar detalles por {User}.",
                    id,
                    GetCurrentUserName());
                return NotFound();
            }

            var canManageLifecycle = await CanManageCategoriesLifecycleAsync();
            if (!category.IsActive && !canManageLifecycle)
            {
                _logger.LogWarning(
                    "Acceso denegado: usuario {User} intentó ver la categoría inactiva {CategoryId}.",
                    GetCurrentUserName(),
                    id);
                return Forbid();
            }

            var canViewInactiveProducts = await CanManageProductsLifecycleAsync();
            var visibleProducts = (canViewInactiveProducts
                    ? category.Products
                    : category.Products.Where(product => product.IsActive))
                .ToList();

            ViewBag.Products = visibleProducts;
            ViewBag.VisibleProducts = visibleProducts;
            ViewBag.CanViewInactiveProducts = canViewInactiveProducts;
            ViewBag.CanManageLifecycle = canManageLifecycle;

            _logger.LogInformation(
                "Usuario {User} consultó detalle de categoría {CategoryId} ({CategoryName}).",
                GetCurrentUserName(),
                id,
                category.Name);

            return View(category);
        }

        [Authorize(Policy = AppPermissions.CategoriesCreate)]
        public async Task<IActionResult> Create()
        {
            await PopulateProductsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.CategoriesCreate)]
        public async Task<IActionResult> Create(CategoryInput category, List<Guid>? selectedProductIds)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Intento de creación de categoría inválido por {User}. Errores: {ErrorCount}.",
                    GetCurrentUserName(),
                    ModelState.ErrorCount);

                await PopulateProductsAsync();
                return View(category);
            }

            try
            {
                var createdCategory = await _categoryApplication.CreateAsync(category, selectedProductIds);
                _logger.LogInformation(
                    "Categoría {CategoryId} ({CategoryName}) creada por {User}.",
                    createdCategory.Id,
                    createdCategory.Name,
                    GetCurrentUserName());

                TempData["SuccessMessage"] = "Categoría creada correctamente.";
                return RedirectToAction(nameof(Edit), new
                {
                    id = createdCategory.Id
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error de negocio al crear la categoría {CategoryName} por {User}.",
                    category.Name,
                    GetCurrentUserName());

                ModelState.AddModelError(nameof(category.Name), ex.Message);
                await PopulateProductsAsync();
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al crear la categoría {CategoryName} por {User}.",
                    category.Name,
                    GetCurrentUserName());

                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al crear la categoría. Por favor, inténtalo nuevamente.");
                await PopulateProductsAsync();
                return View(category);
            }
        }

        [Authorize(Policy = AppPermissions.CategoriesEdit)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var category = await _categoryQueries.GetByIdWithProductsAsync(id);
            if (category == null)
            {
                _logger.LogWarning(
                    "Categoría {CategoryId} no encontrada al abrir edición por {User}.",
                    id,
                    GetCurrentUserName());
                return NotFound();
            }

            await PopulateProductsAsync();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.CategoriesEdit)]
        public async Task<IActionResult> Edit(Guid id, CategoryInput category, List<Guid>? selectedProductIds)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Intento de edición inválida de la categoría {CategoryId} por {User}. Errores: {ErrorCount}.",
                    id,
                    GetCurrentUserName(),
                    ModelState.ErrorCount);

                return await BuildEditValidationViewAsync(id, category);
            }

            try
            {
                var updatedCategory = await _categoryApplication.UpdateAsync(
                    id,
                    category,
                    selectedProductIds ?? new List<Guid>());

                if (updatedCategory == null)
                {
                    _logger.LogWarning(
                        "Categoría {CategoryId} no encontrada al actualizar por {User}.",
                        id,
                        GetCurrentUserName());
                    return NotFound();
                }

                _logger.LogInformation(
                    "Categoría {CategoryId} ({CategoryName}) actualizada por {User}.",
                    id,
                    updatedCategory.Name,
                    GetCurrentUserName());

                TempData["SuccessMessage"] = "Categoría actualizada correctamente.";
                return RedirectToAction(nameof(Edit), new
                {
                    id
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error de negocio al actualizar la categoría {CategoryId} por {User}.",
                    id,
                    GetCurrentUserName());

                var errorField = ex.Message.Contains("nombre", StringComparison.OrdinalIgnoreCase)
                    ? nameof(category.Name)
                    : string.Empty;

                ModelState.AddModelError(errorField, ex.Message);
                return await BuildEditValidationViewAsync(id, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al actualizar la categoría {CategoryId} por {User}.",
                    id,
                    GetCurrentUserName());

                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al actualizar la categoría. Por favor, inténtalo nuevamente.");
                return await BuildEditValidationViewAsync(id, category);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.CategoriesLifecycle)]
        public Task<IActionResult> Deactivate(Guid id) =>
            DeactivateCategoryAsync(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.CategoriesLifecycle)]
        public Task<IActionResult> Delete(Guid id) =>
            DeactivateCategoryAsync(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.CategoriesLifecycle)]
        public Task<IActionResult> Restore(Guid id) =>
            ActivateCategoryAsync(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.CategoriesLifecycle)]
        public Task<IActionResult> Activate(Guid id) =>
            ActivateCategoryAsync(id);

        private async Task<IActionResult> DeactivateCategoryAsync(Guid id)
        {
            try
            {
                var deactivated = await _categoryLifecycle.DeleteAsync(id);
                if (!deactivated)
                {
                    _logger.LogWarning(
                        "No se pudo desactivar la categoría {CategoryId} (tiene productos relacionados, no existe o ya está inactiva). Usuario: {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["ErrorMessage"] = "No se puede desactivar una categoría relacionada con productos, inexistente o ya inactiva.";
                }
                else
                {
                    _logger.LogInformation(
                        "Categoría {CategoryId} desactivada por {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["SuccessMessage"] = "Categoría desactivada correctamente.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al desactivar la categoría {CategoryId} por {User}.",
                    id,
                    GetCurrentUserName());
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al desactivar la categoría.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ActivateCategoryAsync(Guid id)
        {
            try
            {
                var restored = await _categoryLifecycle.RestoreAsync(id);
                if (!restored)
                {
                    _logger.LogWarning(
                        "No se pudo reactivar la categoría {CategoryId} (no existe o ya está activa). Usuario: {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["ErrorMessage"] = "No se pudo reactivar la categoría porque no existe o ya se encuentra activa.";
                }
                else
                {
                    _logger.LogInformation(
                        "Categoría {CategoryId} reactivada por {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["SuccessMessage"] = "Categoría reactivada correctamente.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al reactivar la categoría {CategoryId} por {User}.",
                    id,
                    GetCurrentUserName());
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al reactivar la categoría.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> BuildEditValidationViewAsync(Guid id, CategoryInput category)
        {
            var invalidCategory = await _categoryQueries.GetByIdWithProductsAsync(id);
            if (invalidCategory == null)
            {
                return NotFound();
            }

            invalidCategory.UpdateDetails(
                category.Name ?? string.Empty,
                category.Description ?? string.Empty);

            if (category.IsActive)
            {
                invalidCategory.Activate();
            }
            else
            {
                invalidCategory.Deactivate();
            }

            await PopulateProductsAsync();
            return View(invalidCategory);
        }

        private async Task PopulateProductsAsync()
        {
            ViewBag.Products = await _productQueries.GetAllAsync();
        }

        private async Task<bool> CanManageCategoriesLifecycleAsync() =>
            (await _authorizationService.AuthorizeAsync(User, AppPermissions.CategoriesLifecycle)).Succeeded;

        private async Task<bool> CanManageProductsLifecycleAsync() =>
            (await _authorizationService.AuthorizeAsync(User, AppPermissions.ProductsLifecycle)).Succeeded;

        private string GetCurrentUserName() => User.Identity?.Name ?? "Desconocido";
    }
}
