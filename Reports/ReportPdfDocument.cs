using FinSyncNexus.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinSyncNexus.Reports;

public class ReportPdfDocument : IDocument
{
    private readonly ReportViewModel _model;

    public ReportPdfDocument(ReportViewModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata()
    {
        return DocumentMetadata.Default;
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Header().Element(BuildHeader);
            page.Content().Element(BuildCover);
            page.Footer().AlignCenter().Text("Page 1");
        });

        container.Page(page =>
        {
            page.Margin(40);
            page.Header().Element(BuildHeader);
            page.Content().Element(BuildSummary);
            page.Footer().AlignCenter().Text("Page 2");
        });

        container.Page(page =>
        {
            page.Margin(40);
            page.Header().Element(BuildHeader);
            page.Content().Element(BuildCharts);
            page.Footer().AlignCenter().Text("Page 3");
        });

        container.Page(page =>
        {
            page.Margin(40);
            page.Header().Element(BuildHeader);
            page.Content().Element(BuildTransactions);
            page.Footer().AlignCenter().Text("Page 4");
        });

        container.Page(page =>
        {
            page.Margin(40);
            page.Header().Element(BuildHeader);
            page.Content().Element(BuildObservations);
            page.Footer().AlignCenter().Text("Page 5");
        });
    }

    private void BuildHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("FinSync Nexus").FontSize(16).SemiBold();
                column.Item().Text(_model.ReportTitle).FontSize(12).FontColor(Colors.Grey.Medium);
            });
            row.ConstantItem(160).AlignRight().Text(DateTime.UtcNow.ToString("MMM dd, yyyy"));
        });
    }

    private void BuildCover(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Text("Financial Report").FontSize(22).SemiBold();
            column.Item().Text($"Date Range: {_model.Filters.DateRangeLabel}");
            column.Item().Text($"Generated: {DateTime.UtcNow:MMM dd, yyyy}");
            column.Item().PaddingTop(20).Text("Project Logo Placeholder")
                .FontSize(12).FontColor(Colors.Grey.Medium);
        });
    }

    private void BuildSummary(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Financial Summary").FontSize(16).SemiBold();
            column.Item().Text($"Total Income: {_model.TotalIncome:C}");
            column.Item().Text($"Total Expenses: {_model.TotalExpenses:C}");
            column.Item().Text($"Net Profit / Loss: {_model.NetProfit:C}");
            column.Item().Text($"Cash Balance: {_model.CashBalance:C}");
            column.Item().Text($"Software Included: Xero, QBO");
        });
    }

    private void BuildCharts(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Text("Charts Overview").FontSize(16).SemiBold();

            column.Item().Text("Profit & Loss Trend").SemiBold();
            column.Item().Element(c => BuildChartTable(c, _model.ProfitLossTrend, "Income", "Expense"));

            column.Item().Text("Expense Breakdown").SemiBold();
            column.Item().Element(BuildExpenseBreakdownTable);
        });
    }

    private void BuildTransactions(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Detailed Transactions").FontSize(16).SemiBold();
            column.Item().Element(BuildTransactionTable);
        });
    }

    private void BuildObservations(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Observations").FontSize(16).SemiBold();
            column.Item().Text($"- {_model.Alerts.FirstOrDefault() ?? "No alerts generated."}");
            if (_model.Alerts.Count > 1)
            {
                foreach (var alert in _model.Alerts.Skip(1))
                {
                    column.Item().Text($"- {alert}");
                }
            }
            column.Item().Text($"- Highest expense category: {_model.HighestExpenseCategory}");
        });
    }

    private void BuildExpenseBreakdownTable(IContainer container)
    {
        var data = _model.ExpenseBreakdown.Take(6).ToList();
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(100);
            });
            table.Header(header =>
            {
                header.Cell().Text("Category").SemiBold();
                header.Cell().AlignRight().Text("Amount").SemiBold();
            });
            foreach (var item in data)
            {
                table.Cell().Text(item.Account);
                table.Cell().AlignRight().Text(item.Amount.ToString("C"));
            }
        });
    }

    private void BuildChartTable(IContainer container, List<ReportTrendPoint> points, string left, string right)
    {
        var data = points.Take(6).ToList();
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(90);
                columns.ConstantColumn(90);
            });
            table.Header(header =>
            {
                header.Cell().Text("Month").SemiBold();
                header.Cell().AlignRight().Text(left).SemiBold();
                header.Cell().AlignRight().Text(right).SemiBold();
            });
            foreach (var item in data)
            {
                table.Cell().Text(item.Label);
                table.Cell().AlignRight().Text(item.Income.ToString("C"));
                table.Cell().AlignRight().Text(item.Expense.ToString("C"));
            }
        });
    }

    private void BuildTransactionTable(IContainer container)
    {
        var rows = _model.Transactions.Take(12).ToList();
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70);
                columns.RelativeColumn();
                columns.ConstantColumn(90);
                columns.ConstantColumn(70);
            });
            table.Header(header =>
            {
                header.Cell().Text("Date").SemiBold();
                header.Cell().Text("Description").SemiBold();
                header.Cell().AlignRight().Text("Amount").SemiBold();
                header.Cell().Text("Source").SemiBold();
            });
            foreach (var row in rows)
            {
                table.Cell().Text(row.Date.ToString("MM/dd"));
                table.Cell().Text(row.Description);
                table.Cell().AlignRight().Text(row.Amount.ToString("C"));
                table.Cell().Text(row.Source);
            }
        });
    }
}
