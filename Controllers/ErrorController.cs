using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TechStore.Logging;
using TechStore.Middleware;
using TechStore.Models;

namespace TechStore.Controllers
{
    /// <summary>
    /// Controlador anónimo responsable de renderizar páginas de error personalizadas
    /// en todos los entornos (incluido Development).
    /// </summary>
    [AllowAnonymous]
    public sealed class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;
        private readonly IWebHostEnvironment _environment;

        public ErrorController(
            ILogger<ErrorController> logger,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        [Route("Error")]
        [Route("Error/{statusCode:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult HandleStatusCode(int? statusCode = null)
        {
            var statusCodeFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            var resolvedStatusCode = ResolveStatusCode(statusCode, exceptionFeature);
            var originalPath = ResolveOriginalPath(statusCodeFeature, exceptionFeature);
            var requestId = ResolveTraceId();
            var (title, message) = ResolveErrorCopy(resolvedStatusCode);

            var (exceptionType, exceptionMessage, stackTrace) = ResolveDiagnostics(exceptionFeature);

            HttpContext.Response.StatusCode = resolvedStatusCode;

            LogHandledError(resolvedStatusCode, originalPath, requestId);

            var model = new ErrorDetailsViewModel
            {
                StatusCode = resolvedStatusCode,
                Title = title,
                Message = message,
                RequestId = requestId,
                OriginalPath = originalPath,
                ShowDiagnostics = _environment.IsDevelopment(),
                ExceptionType = exceptionType,
                ExceptionMessage = exceptionMessage,
                StackTrace = stackTrace
            };

            ViewData["Title"] = $"{model.StatusCode} - {model.Title}";

            return View("StatusCode", model);
        }

        private int ResolveStatusCode(int? statusCode, IExceptionHandlerPathFeature? exceptionFeature)
        {
            if (statusCode.HasValue && statusCode.Value >= 400 && statusCode.Value <= 599)
            {
                return statusCode.Value;
            }

            if (exceptionFeature is not null || HttpContext.Items.ContainsKey(ErrorContextKeys.HandlingException))
            {
                return StatusCodes.Status500InternalServerError;
            }

            if (HttpContext.Response.StatusCode >= 400 && HttpContext.Response.StatusCode <= 599)
            {
                return HttpContext.Response.StatusCode;
            }

            return StatusCodes.Status500InternalServerError;
        }

        private string? ResolveOriginalPath(
            IStatusCodeReExecuteFeature? statusCodeFeature,
            IExceptionHandlerPathFeature? exceptionFeature)
        {
            if (HttpContext.Items.TryGetValue(ErrorContextKeys.OriginalPath, out var itemPath)
                && itemPath is string middlewarePath
                && !string.IsNullOrWhiteSpace(middlewarePath))
            {
                return middlewarePath;
            }

            if (!string.IsNullOrWhiteSpace(exceptionFeature?.Path))
            {
                return exceptionFeature.Path;
            }

            if (statusCodeFeature is not null && !string.IsNullOrWhiteSpace(statusCodeFeature.OriginalPath))
            {
                return statusCodeFeature.OriginalPath + statusCodeFeature.OriginalQueryString;
            }

            return null;
        }

        private string ResolveTraceId()
        {
            if (HttpContext.Items.TryGetValue(ErrorContextKeys.TraceId, out var itemTraceId)
                && itemTraceId is string middlewareTraceId
                && !string.IsNullOrWhiteSpace(middlewareTraceId))
            {
                return middlewareTraceId;
            }

            return Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }

        private (string? ExceptionType, string? ExceptionMessage, string? StackTrace) ResolveDiagnostics(
            IExceptionHandlerPathFeature? exceptionFeature)
        {
            if (!_environment.IsDevelopment())
            {
                return (null, null, null);
            }

            var exceptionType = HttpContext.Items.TryGetValue(ErrorContextKeys.ExceptionType, out var typeObj)
                ? typeObj as string
                : exceptionFeature?.Error.GetType().FullName ?? exceptionFeature?.Error.GetType().Name;

            var exceptionMessage = HttpContext.Items.TryGetValue(ErrorContextKeys.ExceptionMessage, out var msgObj)
                ? msgObj as string
                : exceptionFeature?.Error.Message;

            var stackTrace = HttpContext.Items.TryGetValue(ErrorContextKeys.StackTrace, out var stackObj)
                ? stackObj as string
                : exceptionFeature?.Error.ToString();

            return (exceptionType, exceptionMessage, stackTrace);
        }

        private void LogHandledError(int statusCode, string? originalPath, string requestId)
        {
            var user = User.Identity?.IsAuthenticated == true
                ? (User.Identity.Name ?? "Authenticated")
                : "Anonymous";
            var pathForLog = originalPath ?? HttpContext.Request.Path.Value ?? "/Error";

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(
                    AppLogEvents.HttpStatusCodeHandled,
                    "Error page HTTP {StatusCode} rendered for {Path} (User: {User}, TraceId: {TraceId})",
                    statusCode,
                    pathForLog,
                    user,
                    requestId);
                return;
            }

            _logger.LogWarning(
                AppLogEvents.HttpStatusCodeHandled,
                "Status page HTTP {StatusCode} rendered for {Path} (User: {User}, TraceId: {TraceId})",
                statusCode,
                pathForLog,
                user,
                requestId);
        }

        private static (string Title, string Message) ResolveErrorCopy(int statusCode) => statusCode switch
        {
            StatusCodes.Status400BadRequest => (
                "Solicitud incorrecta",
                "La solicitud enviada contiene datos inválidos o no pudo ser procesada. Verifica la información e inténtalo nuevamente."
            ),
            StatusCodes.Status401Unauthorized => (
                "Autenticación requerida",
                "Debes iniciar sesión con tu cuenta de TechStore para acceder a este recurso."
            ),
            StatusCodes.Status403Forbidden => (
                "Acceso denegado",
                "No cuentas con los permisos necesarios para acceder a esta sección o realizar esta operación."
            ),
            StatusCodes.Status404NotFound => (
                "Página no encontrada",
                "El recurso o la página que estás buscando no existe, fue movido o ya no se encuentra disponible."
            ),
            StatusCodes.Status500InternalServerError => (
                "Error interno del servidor",
                "Ocurrió un problema inesperado al procesar tu solicitud. Puedes compartir el código de seguimiento con el equipo de soporte."
            ),
            _ => (
                $"Error HTTP {statusCode}",
                $"Se produjo un inconveniente al procesar tu solicitud (código de estado {statusCode})."
            )
        };
    }
}
