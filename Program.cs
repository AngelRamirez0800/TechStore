using Microsoft.EntityFrameworkCore;
using TechStore.Data;
using TechStore.Data.Repositories;
using TechStore.Data.Storage;
using TechStore.Interfaces;
using TechStore.Logging;
using TechStore.Security.Authorization;
using TechStore.Security.Identity;
using TechStore.Services;

var builder = WebApplication.CreateBuilder(args);

// Logging nativo, auditoría MVC y manejo de excepciones.
builder.AddAuditAndLogging();

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentityConfiguration();
builder.Services.AddAuthorizationPolicies();

builder.Services.AddScoped<IProductsRepository, ProductsRepository>();
builder.Services.AddScoped<ICategoryRespository, CategoryRepository>();

builder.Services.AddScoped<IProductsService, ProductsService>();
builder.Services.AddScoped<IProductApplicationService, ProductApplicationService>();

builder.Services.AddScoped<IProductQueries>(provider =>
    (IProductQueries)provider.GetRequiredService<IProductsService>());
builder.Services.AddScoped<IProductLifecycle>(provider =>
    (IProductLifecycle)provider.GetRequiredService<IProductsService>());

builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<IImageFileValidator, ImageFileValidator>();
builder.Services.AddScoped<IProductImageStorage, LocalProductImageStorage>();
builder.Services.AddScoped<ICategoryProductRepository, CategoryProductRepository>();
builder.Services.AddScoped<ICategoryProductService, CategoryProductService>();
builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICategoryQueries>(provider =>
    (ICategoryQueries)provider.GetRequiredService<ICategoryService>());
builder.Services.AddScoped<ICategoryApplicationService>(provider =>
    (ICategoryApplicationService)provider.GetRequiredService<ICategoryService>());
builder.Services.AddScoped<ICategoryLifecycle>(provider =>
    (ICategoryLifecycle)provider.GetRequiredService<ICategoryService>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
}

await app.SeedIdentityAsync();

// Configure the HTTP request pipeline (activo en todos los entornos, incluido Development).
app.UseGlobalExceptionHandling();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
