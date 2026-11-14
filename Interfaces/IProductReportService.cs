
namespace TechStore.Interfaces
{
    public interface IProductReportService
    {
        byte[] GenerateProductsPdf(
            IReadOnlyCollection<TechStore.Models.Product> products);

        byte[] GenerateProductsExcel(
            IReadOnlyCollection<TechStore.Models.Product> products);
    }
}
