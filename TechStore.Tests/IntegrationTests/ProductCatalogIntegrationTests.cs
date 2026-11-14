
using Microsoft.EntityFrameworkCore;
using TechStore.Data;
using TechStore.Data.Repositories;
using TechStore.Models;
using Xunit;

namespace TechStore.Tests.IntegrationTests;

public class ProductCatalogIntegrationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TechStoreTests_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task AddAsync_DebeGuardarProductoEnBaseDeDatos()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new ProductsRepository(context);

        var product = new Product(
            "Laptop Lenovo",
            "Laptop para pruebas de integración",
            850m,
            15,
            false
        );

        product.MarkCreated();

        // Act
        var productoGuardado = await repository.AddAsync(product);
        var productos = await repository.GetAllAsync();

        // Assert
        Assert.NotEqual(Guid.Empty, productoGuardado.Id);
        Assert.Single(productos);
        Assert.Equal("Laptop Lenovo", productos[0].Name);
        Assert.Equal(850m, productos[0].Price);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_DebeIncluirCategoriasEImagenes()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new ProductsRepository(context);

        var category = new Category(
            "Computadoras",
            "Categoría de equipos informáticos"
        );

        category.MarkCreated();

        var product = new Product(
            "Laptop HP",
            "Laptop de prueba con accesorios",
            950m,
            10,
            false
        );

        product.MarkCreated();
        product.ReplaceCategories(new List<Category> { category });

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var image = new ProductImage(
            product.Id,
            "/images/laptop-hp.jpg",
            1,
            "laptop-hp.jpg",
            250000
        );

        context.ProductImages.Add(image);
        await context.SaveChangesAsync();

        // Act
        var resultado = await repository.GetByIdWithDetailsAsync(product.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Laptop HP", resultado.Name);
        Assert.Single(resultado.Categories);
        Assert.Equal("Computadoras", resultado.Categories.First().Name);
        Assert.Single(resultado.Images);
        Assert.Equal("laptop-hp.jpg", resultado.Images.First().OriginalFileName);
    }

    [Fact]
    public async Task GetAllActiveAsync_DebeRetornarSoloProductosActivos()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new ProductsRepository(context);

        var productoActivo = new Product(
            "Mouse Logitech",
            "Mouse inalámbrico",
            25m,
            20,
            false,
            true
        );

        productoActivo.MarkCreated();

        var productoInactivo = new Product(
            "Teclado antiguo",
            "Producto desactivado",
            30m,
            0,
            false,
            false
        );

        productoInactivo.MarkCreated();

        context.Products.AddRange(
            productoActivo,
            productoInactivo
        );

        await context.SaveChangesAsync();

        // Act
        var resultado = await repository.GetAllActiveAsync();

        // Assert
        Assert.Single(resultado);
        Assert.Equal("Mouse Logitech", resultado.First().Name);
        Assert.All(resultado, producto =>
            Assert.True(producto.IsActive));
    }
}
