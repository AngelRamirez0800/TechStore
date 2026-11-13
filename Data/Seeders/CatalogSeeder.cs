using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TechStore.DTOs;
using TechStore.Interfaces;

namespace TechStore.Data.Seeders
{
    /// <summary>
    /// Inicializa de forma idempotente (o mediante reinicio explícito) el catálogo de categorías,
    /// productos e imágenes reales en base de datos y almacenamiento local.
    /// </summary>
    public sealed class CatalogSeeder : ICatalogSeeder
    {
        private const long MaxImageFileSizeBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private readonly ApplicationDbContext _dbContext;
        private readonly ICategoryService _categoryService;
        private readonly IProductApplicationService _productApplicationService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CatalogSeeder> _logger;

        public CatalogSeeder(
            ApplicationDbContext dbContext,
            ICategoryService categoryService,
            IProductApplicationService productApplicationService,
            IWebHostEnvironment environment,
            ILogger<CatalogSeeder> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _productApplicationService = productApplicationService ?? throw new ArgumentNullException(nameof(productApplicationService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAsync(bool resetExistingData = false, CancellationToken cancellationToken = default)
        {
            if (!resetExistingData)
            {
                var hasCategories = await _dbContext.Categories.AnyAsync(cancellationToken);
                var hasProducts = await _dbContext.Products.AnyAsync(cancellationToken);

                if (hasCategories || hasProducts)
                {
                    _logger.LogInformation(
                        "Seed de catálogo omitido: ya existen registros (Categories={HasCategories}, Products={HasProducts}).",
                        hasCategories,
                        hasProducts);
                    return;
                }
            }

            var seedImagesDirectory = ResolveSeedImagesDirectory();
            ValidateSeedDefinition(seedImagesDirectory);

            var preExistingGuidDirectories = GetExistingGuidDirectoryNames();
            var createdProductIds = new HashSet<Guid>();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (resetExistingData)
                {
                    await ClearCatalogDatabaseAsync(cancellationToken);
                }

                var categoryIdsByName = await SeedCategoriesAsync(cancellationToken);
                await SeedProductsAsync(seedImagesDirectory, categoryIdsByName, createdProductIds, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                CleanupCreatedDirectoriesOnRollback(preExistingGuidDirectories);
                throw;
            }

            if (resetExistingData)
            {
                CleanupObsoleteGuidDirectories(createdProductIds);
            }

            _logger.LogInformation(
                "Seed de catálogo completado correctamente. Categorías: {CategoryCount}, Productos: {ProductCount}, Imágenes: {ImageCount}.",
                CatalogSeedData.Categories.Count,
                CatalogSeedData.Products.Count,
                CatalogSeedData.Products.Sum(p => p.ImageFileNames.Count));
        }

        private async Task ClearCatalogDatabaseAsync(CancellationToken cancellationToken)
        {
            var deletedImages = await _dbContext.ProductImages.ExecuteDeleteAsync(cancellationToken);
            var deletedLinks = await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [CategoryProduct];", cancellationToken);
            var deletedProducts = await _dbContext.Products.ExecuteDeleteAsync(cancellationToken);
            var deletedCategories = await _dbContext.Categories.ExecuteDeleteAsync(cancellationToken);

            _dbContext.ChangeTracker.Clear();

            _logger.LogInformation(
                "Catálogo anterior eliminado antes del seed (ProductImages={Images}, CategoryProduct={Links}, Products={Products}, Categories={Categories}).",
                deletedImages,
                deletedLinks,
                deletedProducts,
                deletedCategories);
        }

        private async Task<Dictionary<string, Guid>> SeedCategoriesAsync(CancellationToken cancellationToken)
        {
            var categoryIdsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            foreach (var categorySeed in CatalogSeedData.Categories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var createdCategory = await _categoryService.CreateAsync(
                    new CategoryInput
                    {
                        Name = categorySeed.Name,
                        Description = categorySeed.Description,
                        IsActive = categorySeed.IsActive
                    },
                    productIds: null);

                categoryIdsByName[createdCategory.Name] = createdCategory.Id;
            }

            return categoryIdsByName;
        }

        private async Task SeedProductsAsync(
            string seedImagesDirectory,
            IReadOnlyDictionary<string, Guid> categoryIdsByName,
            ISet<Guid> createdProductIds,
            CancellationToken cancellationToken)
        {
            foreach (var productSeed in CatalogSeedData.Products)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var categoryIds = productSeed.CategoryNames
                    .Select(categoryName => categoryIdsByName[categoryName])
                    .Distinct()
                    .ToList();

                var streams = new List<FileStream>(productSeed.ImageFileNames.Count);
                try
                {
                    var formFiles = new List<IFormFile>(productSeed.ImageFileNames.Count);
                    foreach (var fileName in productSeed.ImageFileNames)
                    {
                        var fullPath = Path.Combine(seedImagesDirectory, fileName);
                        var stream = File.OpenRead(fullPath);
                        streams.Add(stream);

                        formFiles.Add(new FormFile(stream, 0, stream.Length, "images", fileName)
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = GetContentType(Path.GetExtension(fileName))
                        });
                    }

                    var input = new ProductInput
                    {
                        Name = productSeed.Name,
                        Description = productSeed.Description,
                        Price = productSeed.Price,
                        Stock = productSeed.Stock,
                        Featured = productSeed.Featured,
                        IsActive = productSeed.IsActive
                    };

                    var createdProduct = await _productApplicationService.CreateAsync(input, categoryIds, formFiles);
                    createdProductIds.Add(createdProduct.Id);
                }
                finally
                {
                    foreach (var stream in streams)
                    {
                        await stream.DisposeAsync();
                    }
                }
            }
        }

        private string ResolveSeedImagesDirectory()
        {
            var contentRootCandidate = Path.Combine(_environment.ContentRootPath, "Data", "Seeders", "SeedImages");
            if (Directory.Exists(contentRootCandidate))
            {
                return contentRootCandidate;
            }

            var baseDirCandidate = Path.Combine(AppContext.BaseDirectory, "Data", "Seeders", "SeedImages");
            if (Directory.Exists(baseDirCandidate))
            {
                return baseDirCandidate;
            }

            throw new DirectoryNotFoundException(
                $"No se encontró el directorio de imágenes semilla en '{contentRootCandidate}' ni en '{baseDirCandidate}'.");
        }

        private static void ValidateSeedDefinition(string seedImagesDirectory)
        {
            var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var category in CatalogSeedData.Categories)
            {
                if (string.IsNullOrWhiteSpace(category.Name) || category.Name.Trim().Length is < 2 or > 100)
                {
                    throw new InvalidOperationException($"Nombre de categoría semilla inválido: '{category.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(category.Description) || category.Description.Trim().Length > 500)
                {
                    throw new InvalidOperationException($"Descripción de categoría semilla inválida para '{category.Name}'.");
                }

                if (!categoryNames.Add(category.Name.Trim()))
                {
                    throw new InvalidOperationException($"Categoría duplicada en CatalogSeedData: '{category.Name}'.");
                }
            }

            var productNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var product in CatalogSeedData.Products)
            {
                if (string.IsNullOrWhiteSpace(product.Name))
                {
                    throw new InvalidOperationException("Producto sin nombre en CatalogSeedData.");
                }

                if (!productNames.Add(product.Name.Trim()))
                {
                    throw new InvalidOperationException($"Producto duplicado en CatalogSeedData: '{product.Name}'.");
                }

                if (product.CategoryNames.Count == 0)
                {
                    throw new InvalidOperationException($"El producto '{product.Name}' debe pertenecer al menos a una categoría.");
                }

                foreach (var categoryName in product.CategoryNames)
                {
                    if (!categoryNames.Contains(categoryName))
                    {
                        throw new InvalidOperationException(
                            $"El producto '{product.Name}' referencia una categoría inexistente: '{categoryName}'.");
                    }
                }

                if (product.ImageFileNames.Count == 0)
                {
                    throw new InvalidOperationException($"El producto '{product.Name}' no tiene imágenes semilla asignadas.");
                }

                foreach (var fileName in product.ImageFileNames)
                {
                    ValidateSeedImageFile(Path.Combine(seedImagesDirectory, fileName));
                }
            }
        }

        private static void ValidateSeedImageFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"No se encontró la imagen semilla requerida: '{fullPath}'.", fullPath);
            }

            var extension = Path.GetExtension(fullPath);
            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"Extensión de imagen semilla no permitida: '{fullPath}'.");
            }

            var info = new FileInfo(fullPath);
            if (info.Length <= 0 || info.Length > MaxImageFileSizeBytes)
            {
                throw new InvalidOperationException($"Tamaño de imagen semilla fuera de rango (1B..5MB): '{fullPath}' ({info.Length} bytes).");
            }

            Span<byte> header = stackalloc byte[12];
            using var stream = File.OpenRead(fullPath);
            var bytesRead = stream.Read(header);
            if (bytesRead < 12 || !MatchesExpectedMagicBytes(header, extension))
            {
                throw new InvalidOperationException($"La imagen semilla '{fullPath}' no coincide con una firma binaria válida para '{extension}'.");
            }
        }

        private static bool MatchesExpectedMagicBytes(ReadOnlySpan<byte> header, string extension)
        {
            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            }

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                       header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
            }

            if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            }

            return false;
        }

        private string GetProductsImagesRootPath() =>
            Path.Combine(_environment.WebRootPath, "Images", "Products");

        private HashSet<string> GetExistingGuidDirectoryNames()
        {
            var root = GetProductsImagesRootPath();
            if (!Directory.Exists(root))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return Directory.GetDirectories(root)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name) && Guid.TryParse(name, out _))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private void CleanupObsoleteGuidDirectories(IReadOnlySet<Guid> preservedProductIds)
        {
            var root = GetProductsImagesRootPath();
            if (!Directory.Exists(root))
            {
                return;
            }

            var removedCount = 0;
            foreach (var directory in Directory.GetDirectories(root))
            {
                var folderName = Path.GetFileName(directory);
                if (!Guid.TryParse(folderName, out var folderGuid))
                {
                    continue;
                }

                if (preservedProductIds.Contains(folderGuid))
                {
                    continue;
                }

                Directory.Delete(directory, recursive: true);
                removedCount++;
            }

            if (removedCount > 0)
            {
                _logger.LogInformation("Se limpiaron {RemovedCount} carpeta(s) GUID obsoletas en '{ProductsRoot}'.", removedCount, root);
            }
        }

        private void CleanupCreatedDirectoriesOnRollback(IReadOnlySet<string> preExistingGuidDirectories)
        {
            var root = GetProductsImagesRootPath();
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(root))
            {
                var folderName = Path.GetFileName(directory);
                if (!string.IsNullOrEmpty(folderName) &&
                    Guid.TryParse(folderName, out _) &&
                    !preExistingGuidDirectories.Contains(folderName))
                {
                    try
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo limpiar la carpeta '{Directory}' tras rollback.", directory);
                    }
                }
            }
        }

        private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
