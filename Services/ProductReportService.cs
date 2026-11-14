
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TechStore.Interfaces;
using TechStore.Models;

namespace TechStore.Services
{
    public class ProductReportService : IProductReportService
    {
        public ProductReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateProductsPdf(
            IReadOnlyCollection<Product> products)
        {
            var productList = products.ToList();

            var totalProducts = productList.Count;
            var totalStock = productList.Sum(p => p.Stock);
            var inventoryValue = productList.Sum(p => p.Price * p.Stock);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header()
                        .Column(column =>
                        {
                            column.Item()
                                .Text("TECHSTORE")
                                .FontSize(22)
                                .Bold()
                                .FontColor(Colors.Blue.Darken3);

                            column.Item()
                                .Text("REPORTE DE PRODUCTOS")
                                .FontSize(14)
                                .SemiBold();

                            column.Item()
                                .Text($"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);

                            column.Item()
                                .PaddingVertical(8)
                                .LineHorizontal(1)
                                .LineColor(Colors.Grey.Lighten1);
                        });

                    page.Content()
                        .Column(column =>
                        {
                            column.Spacing(12);

                            column.Item().Row(row =>
                            {
                                row.RelativeItem()
                                    .Text($"Total de productos: {totalProducts}")
                                    .Bold();

                                row.RelativeItem()
                                    .Text($"Unidades en inventario: {totalStock}")
                                    .Bold();

                                row.RelativeItem()
                                    .Text($"Valor del inventario: ${inventoryValue:N2}")
                                    .Bold();
                            });

                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2f);
                                    columns.RelativeColumn(2.5f);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    HeaderCell(header, "PRODUCTO");
                                    HeaderCell(header, "DESCRIPCIÓN");
                                    HeaderCell(header, "CATEGORÍAS");
                                    HeaderCell(header, "PRECIO");
                                    HeaderCell(header, "STOCK");
                                    HeaderCell(header, "ESTADO");
                                    HeaderCell(header, "DESTACADO");
                                });

                                foreach (var product in productList)
                                {
                                    var categories = product.Categories.Any()
                                        ? string.Join(", ",
                                            product.Categories.Select(c => c.Name))
                                        : "Sin categoría";

                                    BodyCell(table, product.Name);
                                    BodyCell(table, product.Description ?? "Sin descripción");
                                    BodyCell(table, categories);
                                    BodyCell(table, $"${product.Price:N2}");
                                    BodyCell(table, product.Stock.ToString());
                                    BodyCell(table,
                                        product.IsActive ? "Activo" : "Inactivo");
                                    BodyCell(table,
                                        product.Featured ? "Sí" : "No");
                                }
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("TechStore - Reporte de productos | Página ");
                            text.CurrentPageNumber();
                            text.Span(" de ");
                            text.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateProductsExcel(
            IReadOnlyCollection<Product> products)
        {
            var productList = products.ToList();

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Productos");

            // Título principal
            worksheet.Cell("A1").Value = "TECHSTORE - REPORTE DE PRODUCTOS";
            worksheet.Range("A1:I1").Merge();

            worksheet.Cell("A2").Value =
                $"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}";

            worksheet.Range("A2:I2").Merge();

            // Encabezados
            var headers = new[]
            {
                "ID",
                "Nombre",
                "Descripción",
                "Categorías",
                "Precio",
                "Stock",
                "Estado",
                "Destacado",
                "Fecha de creación"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(4, i + 1).Value = headers[i];
            }

            // Datos de productos
            int row = 5;

            foreach (var product in productList)
            {
                worksheet.Cell(row, 1).Value = product.Id.ToString();
                worksheet.Cell(row, 2).Value = product.Name;
                worksheet.Cell(row, 3).Value = product.Description;

                worksheet.Cell(row, 4).Value = product.Categories.Any()
                    ? string.Join(", ",
                        product.Categories.Select(c => c.Name))
                    : "Sin categoría";

                worksheet.Cell(row, 5).Value = product.Price;
                worksheet.Cell(row, 6).Value = product.Stock;

                worksheet.Cell(row, 7).Value =
                    product.IsActive ? "Activo" : "Inactivo";

                worksheet.Cell(row, 8).Value =
                    product.Featured ? "Sí" : "No";

                worksheet.Cell(row, 9).Value = product.CreatedAt;

                // Colores alternos para facilitar la lectura
                if (row % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 9)
                        .Style.Fill.BackgroundColor =
                        XLColor.FromHtml("#F2F6FA");
                }

                row++;
            }

            int lastDataRow = row - 1;

            // Estilo del título
            var title = worksheet.Range("A1:I1");

            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 18;
            title.Style.Font.FontColor = XLColor.White;
            title.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#17365D");

            title.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            title.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 32;

            // Fecha de generación
            worksheet.Range("A2:I2").Merge();
            worksheet.Range("A2:I2").Style.Font.Italic = true;
            worksheet.Range("A2:I2").Style.Font.FontColor =
                XLColor.FromHtml("#666666");

            // Encabezados de la tabla
            var headerRange = worksheet.Range("A4:I4");

            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;

            headerRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#24527A");

            headerRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            headerRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            worksheet.Row(4).Height = 25;

            // Formato de los datos
            if (productList.Count > 0)
            {
                var dataRange = worksheet.Range(
                    5, 1, lastDataRow, 9);

                dataRange.Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

                dataRange.Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;

                dataRange.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

                // Formato monetario
                worksheet.Range(5, 5, lastDataRow, 5)
                    .Style.NumberFormat.Format = "$#,##0.00";

                // Formato de fecha
                worksheet.Range(5, 9, lastDataRow, 9)
                    .Style.DateFormat.Format = "dd/mm/yyyy";

                // Alineación de datos numéricos
                worksheet.Range(5, 5, lastDataRow, 6)
                    .Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

                // Centrar estado y destacado
                worksheet.Range(5, 7, lastDataRow, 8)
                    .Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

                // Colores para estados
                for (int currentRow = 5;
                     currentRow <= lastDataRow;
                     currentRow++)
                {
                    var statusCell = worksheet.Cell(currentRow, 7);

                    if (statusCell.GetString() == "Activo")
                    {
                        statusCell.Style.Font.FontColor =
                            XLColor.FromHtml("#198754");
                    }
                    else
                    {
                        statusCell.Style.Font.FontColor =
                            XLColor.FromHtml("#DC3545");
                    }
                }

                // Filtros automáticos
                worksheet.Range(4, 1, lastDataRow, 9)
                    .SetAutoFilter();
            }

            // Resumen del inventario
            int summaryRow = Math.Max(6, row + 2);

            worksheet.Cell(summaryRow, 1).Value =
                "RESUMEN DEL INVENTARIO";

            worksheet.Range(summaryRow, 1, summaryRow, 2).Merge();

            var summaryTitle =
                worksheet.Range(summaryRow, 1, summaryRow, 2);

            summaryTitle.Style.Font.Bold = true;
            summaryTitle.Style.Font.FontColor = XLColor.White;

            summaryTitle.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#17365D");

            worksheet.Cell(summaryRow + 1, 1).Value =
                "Total de productos";

            worksheet.Cell(summaryRow + 1, 2).Value =
                productList.Count;

            worksheet.Cell(summaryRow + 2, 1).Value =
                "Total de unidades";

            worksheet.Cell(summaryRow + 2, 2).Value =
                productList.Sum(p => p.Stock);

            worksheet.Cell(summaryRow + 3, 1).Value =
                "Valor total del inventario";

            worksheet.Cell(summaryRow + 3, 2).Value =
                productList.Sum(p => p.Price * p.Stock);

            worksheet.Cell(summaryRow + 3, 2)
                .Style.NumberFormat.Format = "$#,##0.00";

            // Estilo del resumen
            var summaryRange =
                worksheet.Range(summaryRow + 1, 1, summaryRow + 3, 2);

            summaryRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            summaryRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            summaryRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#EAF1F8");

            worksheet.Range(summaryRow + 1, 1, summaryRow + 3, 1)
                .Style.Font.Bold = true;

            // Ajustes de columnas
            worksheet.Columns().AdjustToContents();

            worksheet.Column(1).Width = 38;
            worksheet.Column(2).Width = 35;
            worksheet.Column(3).Width = 45;
            worksheet.Column(4).Width = 30;
            worksheet.Column(5).Width = 15;
            worksheet.Column(6).Width = 12;
            worksheet.Column(7).Width = 15;
            worksheet.Column(8).Width = 15;
            worksheet.Column(9).Width = 20;

            // Mantener encabezados visibles
            worksheet.SheetView.FreezeRows(4);

            // Configuración de impresión
            worksheet.PageSetup.PageOrientation =
                XLPageOrientation.Landscape;

            worksheet.PageSetup.FitToPages(1, 0);
            worksheet.PageSetup.PagesWide = 1;

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return stream.ToArray();
        }

        private static void HeaderCell(
            TableCellDescriptor header,
            string text)
        {
            header.Cell()
                .Background(Colors.Blue.Darken3)
                .Padding(6)
                .Text(text)
                .FontColor(Colors.White)
                .Bold();
        }

        private static void BodyCell(
            TableDescriptor table,
            string text)
        {
            table.Cell()
                .ShowEntire()
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(5)
                .Text(text);
        }
    }
}
