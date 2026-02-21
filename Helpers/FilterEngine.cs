using FinSyncNexus.Data;
using FinSyncNexus.Models;
using FinSyncNexus.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FinSyncNexus.Helpers;

public static class FilterEngine
{
    public static FilterViewModel Normalize(FilterViewModel filters)
    {
        var (start, end) = ResolveDates(filters);
        filters.FromDate = start;
        filters.ToDate = end;
        filters.DateRangeLabel = $"{start:MMM dd, yyyy} - {end:MMM dd, yyyy}";
        filters.ActiveBadges = BuildBadges(filters);
        return filters;
    }

    public static (DateTime Start, DateTime End) ResolveDates(FilterViewModel filters)
    {
        var today = DateTime.Today;
        var end = filters.ToDate ?? today;
        var start = filters.FromDate ?? today.AddDays(-30);

        switch (filters.DatePreset)
        {
            case "Today":
                start = today;
                end = today;
                break;
            case "ThisMonth":
                start = new DateTime(today.Year, today.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
                break;
            case "Custom":
                break;
            default:
                filters.DatePreset = "ThisMonth";
                start = new DateTime(today.Year, today.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
                break;
        }

        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    public static IQueryable<InvoiceRecord> ApplyInvoiceFilters(
        IQueryable<InvoiceRecord> query,
        FilterViewModel filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Currency) && filters.Currency != "All" && filters.Currency != "USD")
        {
            return query.Where(_ => false);
        }
        if (!string.IsNullOrWhiteSpace(filters.Provider) && filters.Provider != "All")
        {
            query = query.Where(i => i.Provider == filters.Provider);
        }

        if (!string.IsNullOrWhiteSpace(filters.CustomerVendor))
        {
            query = query.Where(i => i.CustomerName.Contains(filters.CustomerVendor));
        }

        if (!string.IsNullOrWhiteSpace(filters.TransactionStatus) &&
            filters.TransactionStatus != "All")
        {
            query = filters.TransactionStatus switch
            {
                "Paid" => query.Where(i => i.Status.ToUpper() == "PAID"),
                "Unpaid" => query.Where(i => i.Status.ToUpper() == "OPEN" || i.Status.ToUpper() == "AUTHORISED"),
                "Overdue" => query.Where(i => i.DueDate.HasValue && i.DueDate.Value.Date < DateTime.Today),
                _ => query
            };
        }

        if (filters.AmountMin.HasValue)
        {
            query = query.Where(i => i.Amount >= filters.AmountMin.Value);
        }

        if (filters.AmountMax.HasValue)
        {
            query = query.Where(i => i.Amount <= filters.AmountMax.Value);
        }

        if (filters.FromDate.HasValue && filters.ToDate.HasValue)
        {
            var start = filters.FromDate.Value.Date;
            var end = filters.ToDate.Value.Date;
            query = query.Where(i =>
                (i.DueDate.HasValue && i.DueDate.Value.Date >= start && i.DueDate.Value.Date <= end) ||
                (i.CreatedAt.Date >= start && i.CreatedAt.Date <= end));
        }

        return query;
    }

    public static IQueryable<ExpenseRecord> ApplyExpenseFilters(
        IQueryable<ExpenseRecord> query,
        FilterViewModel filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Currency) && filters.Currency != "All" && filters.Currency != "USD")
        {
            return query.Where(_ => false);
        }
        if (!string.IsNullOrWhiteSpace(filters.Provider) && filters.Provider != "All")
        {
            query = query.Where(e => e.Provider == filters.Provider);
        }

        if (!string.IsNullOrWhiteSpace(filters.CustomerVendor))
        {
            query = query.Where(e => e.VendorName.Contains(filters.CustomerVendor));
        }

        if (!string.IsNullOrWhiteSpace(filters.AccountType) && filters.AccountType != "All")
        {
            query = query.Where(e => e.Category.Contains(filters.AccountType));
        }

        if (!string.IsNullOrWhiteSpace(filters.TransactionStatus) &&
            filters.TransactionStatus != "All")
        {
            query = filters.TransactionStatus switch
            {
                "Paid" => query.Where(e => e.Status.Contains("Paid") || e.Status.Contains("PAID")),
                "Unpaid" => query.Where(e => e.Status.Contains("Unpaid") || e.Status.Contains("OPEN")),
                "Overdue" => query.Where(e => e.Status.Contains("Overdue")),
                _ => query
            };
        }

        if (filters.AmountMin.HasValue)
        {
            query = query.Where(e => e.Amount >= filters.AmountMin.Value);
        }

        if (filters.AmountMax.HasValue)
        {
            query = query.Where(e => e.Amount <= filters.AmountMax.Value);
        }

        if (filters.FromDate.HasValue && filters.ToDate.HasValue)
        {
            var start = filters.FromDate.Value.Date;
            var end = filters.ToDate.Value.Date;
            query = query.Where(e => e.Date.Date >= start && e.Date.Date <= end);
        }

        return query;
    }

    public static IQueryable<PaymentRecord> ApplyPaymentFilters(
        IQueryable<PaymentRecord> query,
        FilterViewModel filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Currency) && filters.Currency != "All" && filters.Currency != "USD")
        {
            return query.Where(_ => false);
        }
        if (!string.IsNullOrWhiteSpace(filters.Provider) && filters.Provider != "All")
        {
            query = query.Where(p => p.Provider == filters.Provider);
        }

        if (!string.IsNullOrWhiteSpace(filters.CustomerVendor))
        {
            query = query.Where(p => p.CustomerName.Contains(filters.CustomerVendor));
        }

        if (filters.AmountMin.HasValue)
        {
            query = query.Where(p => p.Amount >= filters.AmountMin.Value);
        }

        if (filters.AmountMax.HasValue)
        {
            query = query.Where(p => p.Amount <= filters.AmountMax.Value);
        }

        if (filters.FromDate.HasValue && filters.ToDate.HasValue)
        {
            var start = filters.FromDate.Value.Date;
            var end = filters.ToDate.Value.Date;
            query = query.Where(p => p.Date.Date >= start && p.Date.Date <= end);
        }

        return query;
    }

    public static IQueryable<AccountRecord> ApplyAccountFilters(
        IQueryable<AccountRecord> query,
        FilterViewModel filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Currency) && filters.Currency != "All" && filters.Currency != "USD")
        {
            return query.Where(_ => false);
        }
        if (!string.IsNullOrWhiteSpace(filters.Provider) && filters.Provider != "All")
        {
            query = query.Where(a => a.Provider == filters.Provider);
        }

        if (!string.IsNullOrWhiteSpace(filters.AccountType) && filters.AccountType != "All")
        {
            query = query.Where(a => a.Type == filters.AccountType);
        }

        return query;
    }

    public static async Task<List<string>> GetActiveProvidersAsync(AppDbContext db)
    {
        return await db.Connections.AsNoTracking()
            .Where(c => c.IsConnected)
            .Select(c => c.Provider)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();
    }

    private static List<string> BuildBadges(FilterViewModel filters)
    {
        var badges = new List<string>
        {
            filters.DateRangeLabel
        };

        if (!string.IsNullOrWhiteSpace(filters.Provider) && filters.Provider != "All")
        {
            badges.Add(filters.Provider);
        }

        if (!string.IsNullOrWhiteSpace(filters.CustomerVendor))
        {
            badges.Add($"Customer/Vendor: {filters.CustomerVendor}");
        }

        if (!string.IsNullOrWhiteSpace(filters.AccountType) && filters.AccountType != "All")
        {
            badges.Add($"Account: {filters.AccountType}");
        }

        if (filters.AmountMin.HasValue || filters.AmountMax.HasValue)
        {
            var min = filters.AmountMin?.ToString("C") ?? "-";
            var max = filters.AmountMax?.ToString("C") ?? "-";
            badges.Add($"Amount: {min} - {max}");
        }

        if (!string.IsNullOrWhiteSpace(filters.Currency) && filters.Currency != "All")
        {
            badges.Add($"Currency: {filters.Currency}");
        }

        if (!string.IsNullOrWhiteSpace(filters.TransactionStatus) && filters.TransactionStatus != "All")
        {
            badges.Add($"Status: {filters.TransactionStatus}");
        }

        return badges;
    }
}
