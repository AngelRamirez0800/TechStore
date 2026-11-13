using Microsoft.AspNetCore.Identity;
using TechStore.Data;
using TechStore.Data.Seeders;
using TechStore.Interfaces;
using TechStore.Services;

namespace TechStore.Security.Identity
{
    /// <summary>
    /// Extensiones de configuración para ASP.NET Core Identity, cookies de autenticación y seed inicial.
    /// </summary>
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services)
        {
            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                // Reglas de contraseña compatibles con las cuentas base Angel-247 y Daniel-0800
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 1;

                // Reglas de usuario e inicio de sesión
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;

                // Bloqueo de cuentas por intentos fallidos o acción administrativa
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ReturnUrlParameter = "ReturnUrl";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(4);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            // Permite reflejar cambios de roles, permisos o bloqueo casi de inmediato
            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.FromSeconds(15);
            });

            services.AddScoped<IIdentitySeeder, IdentitySeeder>();
            services.AddScoped<IRoleManagementService, RoleManagementService>();
            services.AddScoped<IUserManagementService, UserManagementService>();

            return services;
        }

        public static Task SeedIdentityAsync(this WebApplication app) =>
            app.Services.SeedIdentityAsync();

        public static async Task SeedIdentityAsync(this IServiceProvider services)
        {
            await using var scope = services.CreateAsyncScope();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("TechStore.Security.Identity.IdentityServiceExtensions");

            try
            {
                var seeder = scope.ServiceProvider.GetRequiredService<IIdentitySeeder>();
                await seeder.SeedAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ocurrió un error al ejecutar el seed inicial de Identity.");
                throw;
            }
        }
    }
}
