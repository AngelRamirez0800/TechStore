
using Moq;
using TechStore.Interfaces;
using TechStore.Models;
using TechStore.Services;
using Xunit;

namespace TechStore.Tests.UnitTests;

public class ProductsServiceTests
{
    private readonly Mock<IProductsRepository> _repositoryMock;
    private readonly ProductsService _service;

    public ProductsServiceTests()
    {
        _repositoryMock = new Mock<IProductsRepository>();
        _service = new ProductsService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_DebeRetornarTodosLosProductos()
    {
        // Arrange
        IList<Product> productos = new List<Product>
        {
            CrearProducto("Laptop Lenovo", "Laptop de prueba", 850m, 5),
            CrearProducto("Mouse Logitech", "Mouse inalámbrico", 25m, 20)
        };

        _repositoryMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(productos);

        // Act
        var resultado = await _service.GetAllAsync();

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Same(productos, resultado);

        _repositoryMock.Verify(
            r => r.GetAllAsync(),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_DebeRetornarProductoExistente()
    {
        // Arrange
        var id = Guid.NewGuid();

        var producto = CrearProducto(
            "Teclado mecánico",
            "Teclado RGB",
            60m,
            8);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(producto);

        // Act
        var resultado = await _service.GetByIdAsync(id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Teclado mecánico", resultado.Name);
        Assert.Same(producto, resultado);

        _repositoryMock.Verify(
            r => r.GetByIdAsync(id),
            Times.Once);
    }

    [Fact]
    public async Task AddAsync_DebeRegistrarProducto()
    {
        // Arrange
        var producto = CrearProducto(
            "Monitor Samsung",
            "Monitor de 24 pulgadas",
            180m,
            10);

        _repositoryMock
            .Setup(r => r.AddAsync(producto))
            .ReturnsAsync(producto);

        // Act
        var resultado = await _service.AddAsync(producto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Monitor Samsung", resultado.Name);

        _repositoryMock.Verify(
            r => r.AddAsync(producto),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DebeActualizarProducto()
    {
        // Arrange
        var producto = CrearProducto(
            "Laptop HP",
            "Laptop actualizada",
            950m,
            7);

        _repositoryMock
            .Setup(r => r.UpdateAsync(producto))
            .ReturnsAsync(producto);

        producto.UpdateDetails(
            "Laptop HP Pro",
            "Laptop actualizada",
            1050m,
            6,
            true);

        // Act
        var resultado = await _service.UpdateAsync(producto);

        // Assert
        Assert.Equal("Laptop HP Pro", resultado.Name);
        Assert.Equal(1050m, resultado.Price);
        Assert.Equal(6, resultado.Stock);

        _repositoryMock.Verify(
            r => r.UpdateAsync(producto),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DebeEliminarProductoExistente()
    {
        // Arrange
        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.DeleteAsync(id))
            .ReturnsAsync(true);

        // Act
        var resultado = await _service.DeleteAsync(id);

        // Assert
        Assert.True(resultado);

        _repositoryMock.Verify(
            r => r.DeleteAsync(id),
            Times.Once);
    }

    private static Product CrearProducto(
        string nombre,
        string descripcion,
        decimal precio,
        int stock)
    {
        var producto = new Product(
            nombre,
            descripcion,
            precio,
            stock,
            featured: false);

        producto.MarkCreated();

        return producto;
    }
}
