namespace TechStore.Models
{
    /// <summary>
    /// Opción individual dentro de un grupo de filtros multiselección.
    /// </summary>
    public sealed record CatalogFilterOption(
        Guid Id,
        string Label,
        bool IsSelected,
        bool IsActive);

    /// <summary>
    /// Modelo de vista compartido para la barra de filtros multiselect en Productos y Categorías.
    /// </summary>
    public sealed class CatalogFilterBarViewModel
    {
        public string ControllerName { get; init; } = string.Empty;
        public string ActionName { get; init; } = "Index";
        public string ButtonLabel { get; init; } = "Filtros";
        public bool ShowPrimaryFilter { get; init; } = true;
        public string PrimaryGroupTitle { get; init; } = string.Empty;
        public string PrimaryParamName { get; init; } = string.Empty;
        public string PrimaryEmptyMessage { get; init; } = "No hay opciones disponibles.";
        public IReadOnlyList<CatalogFilterOption> PrimaryOptions { get; init; } = Array.Empty<CatalogFilterOption>();
        public bool ShowStatusFilter { get; init; }
        public string ActiveStatusLabel { get; init; } = "Activos";
        public string InactiveStatusLabel { get; init; } = "Inactivos";
        public IReadOnlySet<string> SelectedStatuses { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int ResultCount { get; init; }
        public string ResultLabel { get; init; } = "resultados";
    }
}
