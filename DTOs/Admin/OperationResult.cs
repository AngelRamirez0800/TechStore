namespace TechStore.DTOs.Admin
{
    /// <summary>
    /// Resultado estandarizado de una operación administrativa sobre roles o usuarios.
    /// </summary>
    public sealed class OperationResult
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? EntityId { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = [];

        public static OperationResult Ok(string message = "", string? entityId = null) =>
            new()
            {
                Succeeded = true,
                Message = message,
                EntityId = entityId
            };

        public static OperationResult Fail(string message, IEnumerable<string>? errors = null) =>
            new()
            {
                Succeeded = false,
                Message = message,
                Errors = errors?.ToArray() ?? (string.IsNullOrWhiteSpace(message) ? [] : [message])
            };
    }
}
