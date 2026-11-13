using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechStore.Security.Middleware;

namespace TechStore.Logging
{
    /// <summary>
    /// Métodos de extensión para registrar el sistema nativo de logging, auditoría MVC
    /// y manejo global de excepciones y páginas de error.
    /// </summary>
    public static class LoggingAndExceptionExtensions
    {
        /// <summary>
        /// Registra el proveedor nativo de logs diarios en archivo (<c>Logs/techstore-yyyy-MM-dd.log</c>),
        /// configura filtros de nivel y habilita globalmente <see cref="AuditActionFilter"/>.
        /// </summary>
        public static WebApplicationBuilder AddAuditAndLogging(this WebApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var logsDirectory = Path.Combine(builder.Environment.ContentRootPath, "Logs");

            builder.Logging.AddFilter("TechStore", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

            builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(logsDirectory));
            builder.Services.AddScoped<AuditActionFilter>();
            builder.Services.Configure<MvcOptions>(options =>
            {
                options.Filters.AddService<AuditActionFilter>();
            });

            return builder;
        }

        /// <summary>
        /// Activa <see cref="GlobalExceptionMiddleware"/> y <c>UseStatusCodePagesWithReExecute("/Error/{0}")</c>
        /// en todos los entornos (incluido Development).
        /// </summary>
        public static WebApplication UseGlobalExceptionHandling(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            return app;
        }
    }
}
