using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;

namespace TechStore.Logging
{
    /// <summary>
    /// Filtro global de auditoría MVC que registra ejecución, usuario autenticado, argumentos seguros,
    /// duración y código de estado resultante sin exponer datos sensibles.
    /// </summary>
    public sealed class AuditActionFilter : IAsyncActionFilter
    {
        private static readonly string[] SensitiveFieldMarkers =
        [
            "password",
            "token",
            "secret",
            "credential",
            "authorization",
            "cookie",
            "hash",
            "salt"
        ];

        private readonly ILogger<AuditActionFilter> _logger;

        public AuditActionFilter(ILogger<AuditActionFilter> logger)
        {
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = SanitizeLogValue(context.RouteData.Values["controller"]?.ToString()) ?? "Unknown";
            var action = SanitizeLogValue(context.RouteData.Values["action"]?.ToString()) ?? "Unknown";
            var httpMethod = SanitizeLogValue(context.HttpContext.Request.Method) ?? "UNKNOWN";
            var path = SanitizeLogValue(context.HttpContext.Request.Path.Value) ?? "/";
            var user = GetCurrentUserName(context.HttpContext);
            var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            var safeArguments = FormatSafeArguments(context.ActionArguments);

            _logger.LogInformation(
                AppLogEvents.ActionExecuting,
                "Executing {Method} {Path} -> {Controller}.{Action} for {User} | Args: {Arguments} | TraceId: {TraceId}",
                httpMethod,
                path,
                controller,
                action,
                user,
                safeArguments,
                traceId);

            if (!context.ModelState.IsValid)
            {
                var invalidFields = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .Select(entry => SanitizeLogValue(entry.Key) ?? "Model")
                    .ToArray();

                _logger.LogWarning(
                    AppLogEvents.ActionExecuting,
                    "ModelState invalid on {Method} {Path} -> {Controller}.{Action} for {User} | Fields: [{InvalidFields}] | TraceId: {TraceId}",
                    httpMethod,
                    path,
                    controller,
                    action,
                    user,
                    string.Join(", ", invalidFields),
                    traceId);
            }

            var startTimestamp = Stopwatch.GetTimestamp();
            var executedContext = await next();
            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

            if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
            {
                _logger.LogWarning(
                    AppLogEvents.ActionExecuted,
                    executedContext.Exception,
                    "Action {Method} {Path} -> {Controller}.{Action} for {User} failed with {ExceptionType} in {ElapsedMs:F2}ms (TraceId: {TraceId})",
                    httpMethod,
                    path,
                    controller,
                    action,
                    user,
                    executedContext.Exception.GetType().Name,
                    elapsedMs,
                    traceId);
                return;
            }

            var statusCode = ResolveStatusCode(executedContext);

            _logger.LogInformation(
                AppLogEvents.ActionExecuted,
                "Executed {Method} {Path} -> {Controller}.{Action} for {User} responded {StatusCode} in {ElapsedMs:F2}ms (TraceId: {TraceId})",
                httpMethod,
                path,
                controller,
                action,
                user,
                statusCode,
                elapsedMs,
                traceId);
        }

        private static string GetCurrentUserName(HttpContext httpContext)
        {
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                return SanitizeLogValue(httpContext.User.Identity.Name) ?? "Authenticated";
            }

            return "Anonymous";
        }

        private static int ResolveStatusCode(ActionExecutedContext context)
        {
            return context.Result switch
            {
                IStatusCodeActionResult statusCodeResult when statusCodeResult.StatusCode.HasValue
                    => statusCodeResult.StatusCode.Value,
                ForbidResult => StatusCodes.Status403Forbidden,
                ChallengeResult or UnauthorizedResult => StatusCodes.Status401Unauthorized,
                RedirectResult or RedirectToActionResult or RedirectToRouteResult or LocalRedirectResult
                    => StatusCodes.Status302Found,
                _ => context.HttpContext.Response.StatusCode
            };
        }

        private static string FormatSafeArguments(IDictionary<string, object?> arguments)
        {
            if (arguments.Count == 0)
            {
                return "{}";
            }

            var parts = new List<string>(arguments.Count);

            foreach (var (key, value) in arguments)
            {
                var safeKey = SanitizeLogValue(key) ?? "arg";
                var formattedValue = IsSensitiveName(safeKey)
                    ? "\"***\""
                    : FormatValue(value, depth: 0);

                parts.Add($"{safeKey}: {formattedValue}");
            }

            return "{ " + string.Join(", ", parts) + " }";
        }

        private static string FormatValue(object? value, int depth)
        {
            if (value is null)
            {
                return "null";
            }

            switch (value)
            {
                case CancellationToken:
                    return "[CancellationToken]";
                case Stream:
                    return "[Stream]";
                case IFormFile file:
                    return $"File({SanitizeLogValue(file.FileName)}, {file.Length}B)";
                case IEnumerable<IFormFile> files:
                    return $"Files({files.Count()})";
                case string text:
                    var clean = SanitizeLogValue(text) ?? string.Empty;
                    if (clean.Length > 100)
                    {
                        clean = clean[..100] + "...";
                    }

                    return $"\"{clean}\"";
            }

            var type = value.GetType();

            if (type.IsPrimitive || type.IsEnum || value is decimal || value is Guid || value is DateTime || value is DateTimeOffset)
            {
                return SanitizeLogValue(value.ToString()) ?? string.Empty;
            }

            if (value is IEnumerable enumerable)
            {
                return FormatEnumerable(enumerable, depth);
            }

            if (depth >= 1)
            {
                return type.Name;
            }

            return FormatComplexObject(value, type, depth + 1);
        }

        private static string FormatEnumerable(IEnumerable enumerable, int depth)
        {
            const int maxPreviewItems = 8;
            var items = new List<string>(maxPreviewItems);
            var totalCount = 0;

            foreach (var item in enumerable)
            {
                if (totalCount < maxPreviewItems)
                {
                    items.Add(depth < 1 ? FormatValue(item, depth + 1) : (item?.ToString() ?? "null"));
                }

                totalCount++;
            }

            var preview = string.Join(", ", items);
            return totalCount > maxPreviewItems
                ? $"[{preview}, ... ({totalCount} total)]"
                : $"[{preview}]";
        }

        private static string FormatComplexObject(object instance, Type type, int depth)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var builder = new StringBuilder();
            builder.Append("{ ");

            var written = 0;

            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (written > 0)
                {
                    builder.Append(", ");
                }

                var propertyName = property.Name;
                string propertyValue;

                if (IsSensitiveName(propertyName))
                {
                    propertyValue = "\"***\"";
                }
                else
                {
                    try
                    {
                        propertyValue = FormatValue(property.GetValue(instance), depth);
                    }
                    catch
                    {
                        propertyValue = "[Unreadable]";
                    }
                }

                builder.Append(propertyName).Append(": ").Append(propertyValue);
                written++;
            }

            builder.Append(" }");
            return builder.ToString();
        }

        private static bool IsSensitiveName(string name)
        {
            foreach (var marker in SensitiveFieldMarkers)
            {
                if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? SanitizeLogValue(string? value)
        {
            if (value is null)
            {
                return null;
            }

            return value
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
        }
    }
}
