namespace TechStore.Services
{
    /// <summary>
    /// Helper compartido para normalizar y aplicar filtros multiselección de catálogo y estado.
    /// </summary>
    public static class CatalogFilterHelper
    {
        public const string ActiveStatusValue = "active";
        public const string InactiveStatusValue = "inactive";

        public static HashSet<Guid> NormalizeGuids(IEnumerable<Guid>? ids, Guid? singleId = null)
        {
            var result = new HashSet<Guid>();

            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (id != Guid.Empty)
                    {
                        result.Add(id);
                    }
                }
            }

            if (singleId.HasValue && singleId.Value != Guid.Empty)
            {
                result.Add(singleId.Value);
            }

            return result;
        }

        public static HashSet<string> NormalizeStatuses(IEnumerable<string>? statuses, bool allowStatusFilter)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!allowStatusFilter || statuses == null)
            {
                return result;
            }

            foreach (var status in statuses)
            {
                if (string.Equals(status, ActiveStatusValue, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(ActiveStatusValue);
                }
                else if (string.Equals(status, InactiveStatusValue, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(InactiveStatusValue);
                }
            }

            return result;
        }

        public static IEnumerable<T> FilterByStatus<T>(
            IEnumerable<T> items,
            IReadOnlySet<string> selectedStatuses,
            Func<T, bool> isActiveSelector)
        {
            if (selectedStatuses.Count != 1)
            {
                return items;
            }

            var wantActive = selectedStatuses.Contains(ActiveStatusValue);
            return items.Where(item => isActiveSelector(item) == wantActive);
        }
    }
}
