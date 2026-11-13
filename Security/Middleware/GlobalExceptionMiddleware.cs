using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TechStore.Logging;

namespace TechStore.Security.Middleware
{
    /// <summary>
    /// Claves compartidas en <see cref="HttpContext.Items"/> entre <see cref="GlobalExceptionMiddleware"/>
    /// y <c>ErrorController</c>.
    /// </summary>
    internal static class ErrorContextKeys
    {
        public const string HandlingException = "TechStore.Error.HandlingException";
        public const string OriginalPath = "TechStore.Error.OriginalPath";
        public const string TraceId = "TechStore.Error.TraceId";
        public const string ExceptionType = "TechStore.Error.ExceptionType";
        public const string ExceptionMessage = "TechStore.Error.ExceptionMessage";
        public const string StackTrace = "TechStore.Error.StackTrace";
    }

    /// <summary>
    /// Middleware global de manejo de excepciones activo en todos los entornos (incluido Development).
    /// Registra la excepción con TraceId, responde JSON para llamadas API/AJAX o re-ejecuta <c>/Error/500</c>.
    /// </summary>
    public sealed class GlobalExceptionMiddleware
    {
        private static readonly PathString ErrorPath500 = new("/Error/500");

        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
                {
                    var abortedTraceId = Activity.Current?.Id ?? context.TraceIdentifier;
                    _logger.LogDebug(
                        "Request aborted by client on {Method} {Path} (TraceId: {TraceId})",
                        context.Request.Method,
                        BuildOriginalPath(context.Request),
                        abortedTraceId);
                    return;
                }

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var user = context.User.Identity?.IsAuthenticated == true
                ? (context.User.Identity.Name ?? "Authenticated")
                : "Anonymous";
            var originalPath = BuildOriginalPath(context.Request);

            _logger.LogError(
                AppLogEvents.UnhandledException,
                ex,
                "Unhandled exception on {Method} {Path} for user {User} (TraceId: {TraceId})",
                context.Request.Method,
                originalPath,
                user,
                traceId);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    AppLogEvents.UnhandledException,
                    "Response has already started; cannot render error page for {Path} (TraceId: {TraceId})",
                    originalPath,
                    traceId);
                return;
            }

            if (context.Items.ContainsKey(ErrorContextKeys.HandlingException))
            {
                _logger.LogCritical(
                    AppLogEvents.UnhandledException,
                    ex,
                    "Recursive failure while rendering error page for {Path} (TraceId: {TraceId})",
                    originalPath,
                    traceId);

                await WriteFallbackPlainTextResponseAsync(context, traceId);
                return;
            }

            context.Items[ErrorContextKeys.HandlingException] = true;
            context.Response.Clear();

            if (IsJsonOrAjaxRequest(context.Request))
            {
                await WriteProblemDetailsResponseAsync(context, ex, originalPath, traceId);
                return;
            }

            await ReExecuteErrorPageAsync(context, ex, originalPath, traceId);
        }

        private async Task WriteProblemDetailsResponseAsync(
            HttpContext context,
            Exception ex,
            string originalPath,
            string traceId)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json; charset=utf-8";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error interno en el servidor.",
                Detail = _environment.IsDevelopment()
                    ? ex.Message
                    : "Se produjo un error inesperado al procesar la solicitud. Utiliza el identificador de seguimiento para soporte.",
                Instance = originalPath
            };

            problem.Extensions["traceId"] = traceId;

            if (_environment.IsDevelopment())
            {
                problem.Extensions["exceptionType"] = ex.GetType().FullName ?? ex.GetType().Name;
            }

            await JsonSerializer.SerializeAsync(context.Response.Body, problem);
        }

        private async Task ReExecuteErrorPageAsync(
            HttpContext context,
            Exception ex,
            string originalPath,
            string traceId)
        {
            context.Items[ErrorContextKeys.OriginalPath] = originalPath;
            context.Items[ErrorContextKeys.TraceId] = traceId;

            if (_environment.IsDevelopment())
            {
                context.Items[ErrorContextKeys.ExceptionType] = ex.GetType().FullName ?? ex.GetType().Name;
                context.Items[ErrorContextKeys.ExceptionMessage] = ex.Message;
                context.Items[ErrorContextKeys.StackTrace] = ex.ToString();
                context.Features.Set<IExceptionHandlerPathFeature>(new HandledExceptionFeature(ex, originalPath));
                context.Features.Set<IExceptionHandlerFeature>(context.Features.Get<IExceptionHandlerPathFeature>());
            }

            var previousPath = context.Request.Path;
            var previousQuery = context.Request.QueryString;

            try
            {
                context.SetEndpoint(null);
                context.Request.RouteValues.Clear();
                context.Request.Path = ErrorPath500;
                context.Request.QueryString = QueryString.Empty;
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                await _next(context);
            }
            catch (Exception secondaryEx)
            {
                _logger.LogCritical(
                    AppLogEvents.UnhandledException,
                    secondaryEx,
                    "Recursive failure while rendering error page for {Path} (TraceId: {TraceId})",
                    originalPath,
                    traceId);

                if (!context.Response.HasStarted)
                {
                    await WriteFallbackPlainTextResponseAsync(context, traceId);
                }
            }
            finally
            {
                context.Request.Path = previousPath;
                context.Request.QueryString = previousQuery;
            }
        }

        private static async Task WriteFallbackPlainTextResponseAsync(HttpContext context, string traceId)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync($"Error interno del servidor. Código de seguimiento: {traceId}");
        }

        private static bool IsJsonOrAjaxRequest(HttpRequest request)
        {
            if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var accept = request.Headers.Accept.ToString();
            return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildOriginalPath(HttpRequest request)
        {
            var path = request.Path.HasValue ? request.Path.Value! : "/";
            var query = request.QueryString.HasValue ? request.QueryString.Value! : string.Empty;
            return path + query;
        }

        private sealed class HandledExceptionFeature : IExceptionHandlerPathFeature
        {
            public HandledExceptionFeature(Exception error, string path)
            {
                Error = error;
                Path = path;
            }

            public Exception Error { get; }
            public string Path { get; }
        }
    }
}
