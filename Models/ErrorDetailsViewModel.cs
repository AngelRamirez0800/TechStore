namespace TechStore.Models
{
    /// <summary>
    /// Modelo de vista para mostrar páginas de error HTTP y diagnóstico opcional en desarrollo.
    /// </summary>
    public class ErrorDetailsViewModel
    {
        public int StatusCode { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public string? OriginalPath { get; set; }
        public bool ShowDiagnostics { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? StackTrace { get; set; }
    }
}
