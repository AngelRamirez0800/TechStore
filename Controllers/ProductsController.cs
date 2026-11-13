using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.DTOs;
using TechStore.Interfaces;
using TechStore.Models;
using TechStore.Security.Authorization;
using TechStore.Services;

namespace TechStore.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly IProductQueries _productService;
        private readonly IProductApplicationService _productApplicationService;
        private readonly ICategoryQueries _categoryService;
        private readonly IProductLifecycle _productLifecycle;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductQueries productService,
            IProductApplicationService productApplicationService,
            ICategoryQueries categoryService,
            IProductLifecycle productLifecycle,
            IAuthorizationService authorizationService,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _productApplicationService = productApplicationService;
            _categoryService = categoryService;
            _productLifecycle = productLifecycle;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        [Authorize(Policy = AppPermissions.ProductsRead)]
        public async Task<IActionResult> Index(
            List<Guid>? categoryIds = null,
            Guid? categoryId = null,
            List<string>? statuses = null)
        {
            var canManageLifecycle = await CanManageLifecycleAsync();
            var canViewInactiveCategories = await CanManageCategoriesLifecycleAsync();
            var selectedCategoryIds = CatalogFilterHelper.NormalizeGuids(categoryIds, categoryId);
            var selectedStatuses = CatalogFilterHelper.NormalizeStatuses(statuses, canManageLifecycle);

            var products = canManageLifecycle
                ? await _productService.GetAllAsync()
                : await _productService.GetAllActiveAsync();

            var filterCategories = (canViewInactiveCategories
                ? await _categoryService.GetAllAsync()
                : await _categoryService.GetAllActiveAsync())
                .OrderBy(category => category.Name)
                .ToList();

            var filteredProducts = products.AsEnumerable();

            if (selectedCategoryIds.Count > 0)
            {
                filteredProducts = filteredProducts.Where(product =>
                    product.Categories.Any(category =>
                        selectedCategoryIds.Contains(category.Id) &&
                        (canViewInactiveCategories || category.IsActive)));
            }

            filteredProducts = CatalogFilterHelper.FilterByStatus(
                filteredProducts,
                selectedStatuses,
                product => product.IsActive);

            var productList = filteredProducts.ToList();

            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedCategoryIds = selectedCategoryIds;
            ViewBag.SelectedStatuses = selectedStatuses;
            ViewBag.CanManageLifecycle = canManageLifecycle;
            ViewBag.Categories = filterCategories;

            _logger.LogInformation(
                "Usuario {User} consultó productos (Total: {Count}, CategoryIds: {CategoryIds}, Statuses: {Statuses}).",
                GetCurrentUserName(),
                productList.Count,
                string.Join(",", selectedCategoryIds),
                string.Join(",", selectedStatuses));

            return View(productList);
        }

        [Authorize(Policy = AppPermissions.ProductsRead)]
        public async Task<IActionResult> Details(Guid id)
        {
            var product = await _productService.GetByIdWithDetailsAsync(id);
            if (product == null)
            {
                _logger.LogWarning(
                    "Producto {ProductId} no encontrado al consultar detalles por {User}.",
                    id,
                    GetCurrentUserName());
                return NotFound();
            }

            var canManageLifecycle = await CanManageLifecycleAsync();
            if (!product.IsActive && !canManageLifecycle)
            {
                _logger.LogWarning(
                    "Acceso denegado: usuario {User} intentó ver el producto inactivo {ProductId}.",
                    GetCurrentUserName(),
                    id);
                return Forbid();
            }

            _logger.LogInformation(
                "Usuario {User} consultó el detalle del producto {ProductId} ({ProductName}).",
                GetCurrentUserName(),
                id,
                product.Name);

            return View(product);
        }

        [Authorize(Policy = AppPermissions.ProductsCreate)]
        public async Task<IActionResult> Create()
        {
            await PopulateProductOptionsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsCreate)]
        public async Task<IActionResult> Create(ProductInput product, List<Guid>? selectedCategoryIds, ICollection<IFormFile>? images)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Intento de creación de producto inválido por {User}. Errores: {ErrorCount}.",
                    GetCurrentUserName(),
                    ModelState.ErrorCount);
                await PopulateProductOptionsAsync(selectedCategoryIds);
                return View(BuildProductPreview(product));
            }

            try
            {
                var createdProduct = await _productApplicationService.CreateAsync(product, selectedCategoryIds, images);
                _logger.LogInformation(
                    "Producto {ProductId} ({ProductName}) creado por {User}.",
                    createdProduct.Id,
                    createdProduct.Name,
                    GetCurrentUserName());

                TempData["SuccessMessage"] = "Producto creado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error de validación al crear el producto {ProductName} por {User}.",
                    product.Name,
                    GetCurrentUserName());

                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateProductOptionsAsync(selectedCategoryIds);
                return View(BuildProductPreview(product));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al crear el producto {ProductName} por {User}.",
                    product.Name,
                    GetCurrentUserName());

                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al crear el producto. Por favor, inténtalo nuevamente.");
                await PopulateProductOptionsAsync(selectedCategoryIds);
                return View(BuildProductPreview(product));
            }
        }

        [Authorize(Policy = AppPermissions.ProductsEdit)]
        public async Task<IActionResult> Edit(Guid id)
        {
            var product = await _productService.GetByIdWithDetailsAsync(id);
            if (product == null)
            {
                _logger.LogWarning(
                    "Producto {ProductId} no encontrado al abrir edición por {User}.",
                    id,
                    GetCurrentUserName());
                return NotFound();
            }

            await PopulateProductOptionsAsync(
                product.Categories.Select(category => category.Id),
                includeInactiveCategories: true);

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsEdit)]
        public async Task<IActionResult> Edit(Guid id, ProductInput product, List<Guid>? selectedCategoryIds, ICollection<IFormFile>? images)
        {
            var targetId = id != Guid.Empty ? id : product.Id;
            if (targetId == Guid.Empty)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Intento de edición inválida del producto {ProductId} por {User}. Errores: {ErrorCount}.",
                    targetId,
                    GetCurrentUserName(),
                    ModelState.ErrorCount);

                return await BuildEditValidationViewAsync(targetId, product, selectedCategoryIds);
            }

            try
            {
                var updatedProduct = await _productApplicationService.UpdateAsync(
                    targetId,
                    product,
                    selectedCategoryIds ?? new List<Guid>(),
                    images);

                if (updatedProduct == null)
                {
                    _logger.LogWarning(
                        "Producto {ProductId} no encontrado al actualizar por {User}.",
                        targetId,
                        GetCurrentUserName());
                    return NotFound();
                }

                _logger.LogInformation(
                    "Producto {ProductId} ({ProductName}) actualizado por {User}.",
                    targetId,
                    updatedProduct.Name,
                    GetCurrentUserName());

                TempData["SuccessMessage"] = images != null && images.Count > 0
                    ? $"Producto e imágenes ({images.Count}) actualizados correctamente."
                    : "Producto actualizado correctamente.";

                return RedirectToAction(nameof(Edit), new { id = targetId });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error de validación al actualizar el producto {ProductId} por {User}.",
                    targetId,
                    GetCurrentUserName());

                ModelState.AddModelError(string.Empty, ex.Message);
                return await BuildEditValidationViewAsync(targetId, product, selectedCategoryIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al actualizar el producto {ProductId} por {User}.",
                    targetId,
                    GetCurrentUserName());

                TempData["ErrorMessage"] = "Ocurrió un error inesperado al actualizar el producto. Por favor, inténtalo nuevamente.";
                return RedirectToAction(nameof(Edit), new { id = targetId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsEdit)]
        public async Task<IActionResult> AddImages(Guid id, ICollection<IFormFile> images)
        {
            var existingProduct = await _productService.GetByIdWithDetailsAsync(id);
            if (existingProduct == null)
            {
                _logger.LogWarning(
                    "Producto {ProductId} no encontrado al intentar subir imágenes por {User}.",
                    id,
                    GetCurrentUserName());
                return NotFound();
            }

            if (images == null || images.Count == 0)
            {
                TempData["ErrorMessage"] = "Debes seleccionar al menos una imagen.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            try
            {
                await _productApplicationService.AddImagesAsync(id, images);
                _logger.LogInformation(
                    "Se subieron {ImageCount} imagen(es) al producto {ProductId} por {User}.",
                    images.Count,
                    id,
                    GetCurrentUserName());

                TempData["SuccessMessage"] = $"Se han subido {images.Count} imagen(es) correctamente.";
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Archivo inválido al subir imágenes para el producto {ProductId} por {User}.",
                    id,
                    GetCurrentUserName());

                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al subir imágenes para el producto {ProductId} por {User}.",
                    id,
                    GetCurrentUserName());

                TempData["ErrorMessage"] = "Ocurrió un error inesperado al subir las imágenes.";
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsEdit)]
        public Task<IActionResult> RemoveImage(Guid imageId, Guid productId) =>
            RemoveImageInternalAsync(imageId, productId);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsEdit)]
        public Task<IActionResult> DeleteImage(Guid imageId, Guid productId) =>
            RemoveImageInternalAsync(imageId, productId);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsLifecycle)]
        public Task<IActionResult> Deactivate(Guid id) =>
            DeactivateProductAsync(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsLifecycle)]
        public Task<IActionResult> Delete(Guid id) =>
            DeactivateProductAsync(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsLifecycle)]
        public Task<IActionResult> Restore(Guid id) =>
            ActivateProductAsync(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AppPermissions.ProductsLifecycle)]
        public Task<IActionResult> Activate(Guid id) =>
            ActivateProductAsync(id);

        private async Task<IActionResult> RemoveImageInternalAsync(Guid imageId, Guid productId)
        {
            try
            {
                await _productApplicationService.RemoveImageAsync(imageId);
                _logger.LogInformation(
                    "Imagen {ImageId} eliminada del producto {ProductId} por {User}.",
                    imageId,
                    productId,
                    GetCurrentUserName());

                TempData["SuccessMessage"] = "Imagen eliminada correctamente.";
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Imagen {ImageId} no encontrada al intentar eliminarla del producto {ProductId} por {User}.",
                    imageId,
                    productId,
                    GetCurrentUserName());

                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al eliminar la imagen {ImageId} del producto {ProductId} por {User}.",
                    imageId,
                    productId,
                    GetCurrentUserName());

                TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar la imagen.";
            }

            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        private async Task<IActionResult> DeactivateProductAsync(Guid id)
        {
            try
            {
                var deactivated = await _productLifecycle.DeleteAsync(id);
                if (!deactivated)
                {
                    _logger.LogWarning(
                        "No se pudo desactivar el producto {ProductId} (no existe o ya está inactivo). Usuario: {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["ErrorMessage"] = "No se pudo desactivar el producto porque no existe o ya se encuentra inactivo.";
                }
                else
                {
                    _logger.LogInformation(
                        "Producto {ProductId} desactivado por {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["SuccessMessage"] = "Producto desactivado correctamente.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al desactivar el producto {ProductId} por {User}.",
                    id,
                    GetCurrentUserName());
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al desactivar el producto.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ActivateProductAsync(Guid id)
        {
            try
            {
                var restored = await _productLifecycle.RestoreAsync(id);
                if (!restored)
                {
                    _logger.LogWarning(
                        "No se pudo reactivar el producto {ProductId} (no existe o ya está activo). Usuario: {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["ErrorMessage"] = "No se pudo reactivar el producto porque no existe o ya se encuentra activo.";
                }
                else
                {
                    _logger.LogInformation(
                        "Producto {ProductId} reactivado por {User}.",
                        id,
                        GetCurrentUserName());
                    TempData["SuccessMessage"] = "Producto reactivado correctamente.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado al reactivar el producto {ProductId} por {User}.",
                    id,
                    GetCurrentUserName());
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al reactivar el producto.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> BuildEditValidationViewAsync(
            Guid targetId,
            ProductInput product,
            List<Guid>? selectedCategoryIds)
        {
            var existingProduct = await _productService.GetByIdWithDetailsAsync(targetId);
            if (existingProduct == null)
            {
                return NotFound();
            }

            existingProduct.UpdateDetails(
                product.Name ?? string.Empty,
                product.Description ?? string.Empty,
                product.Price,
                product.Stock,
                product.Featured);

            if (product.IsActive)
            {
                existingProduct.Activate();
            }
            else
            {
                existingProduct.Deactivate();
            }

            await PopulateProductOptionsAsync(selectedCategoryIds, includeInactiveCategories: true);
            return View(existingProduct);
        }

        private async Task PopulateProductOptionsAsync(
            IEnumerable<Guid>? selectedCategoryIds = null,
            bool includeInactiveCategories = false)
        {
            ViewBag.Categories = includeInactiveCategories
                ? await _categoryService.GetAllAsync()
                : await _categoryService.GetAllActiveAsync();
            ViewBag.SelectedCategoryIds = selectedCategoryIds ?? Enumerable.Empty<Guid>();
        }

        private async Task<bool> CanManageLifecycleAsync() =>
            (await _authorizationService.AuthorizeAsync(User, AppPermissions.ProductsLifecycle)).Succeeded;

        private async Task<bool> CanManageCategoriesLifecycleAsync() =>
            (await _authorizationService.AuthorizeAsync(User, AppPermissions.CategoriesLifecycle)).Succeeded;

        private string GetCurrentUserName() => User.Identity?.Name ?? "Desconocido";

        private static Product BuildProductPreview(ProductInput product) =>
            new(
                product.Name ?? string.Empty,
                product.Description ?? string.Empty,
                product.Price,
                product.Stock,
                product.Featured,
                product.IsActive);
    }
}
