using FinSyncNexus.Models;

namespace FinSyncNexus.Services;

public class DummyDataService
{
    public List<InvoiceRecord> BuildDummyInvoices(string provider)
    {
        return new List<InvoiceRecord>
        {
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-INV-1001",
                InvoiceNumber = "1001",
                CustomerName = "Demo Retail Store",
                Amount = 362.07m,
                Status = "OPEN",
                DueDate = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-INV-1002",
                InvoiceNumber = "1002",
                CustomerName = "Sample Traders",
                Amount = 477.50m,
                Status = "OPEN",
                DueDate = DateTime.UtcNow.AddDays(10),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-INV-1003",
                InvoiceNumber = "1003",
                CustomerName = "Mark & Co",
                Amount = 314.28m,
                Status = "OPEN",
                DueDate = DateTime.UtcNow.AddDays(5),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-INV-1004",
                InvoiceNumber = "1004",
                CustomerName = "Rondownu Fruit",
                Amount = 78.60m,
                Status = "OPEN",
                DueDate = DateTime.UtcNow.AddDays(3),
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-INV-1005",
                InvoiceNumber = "1005",
                CustomerName = "Geeta Foods",
                Amount = 629.10m,
                Status = "OPEN",
                DueDate = DateTime.UtcNow.AddDays(9),
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    public List<CustomerRecord> BuildDummyCustomers(string provider)
    {
        return new List<CustomerRecord>
        {
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-CUST-101",
                Name = "Demo Retail Store",
                Email = "demo@retail.test",
                Phone = "9990001111",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-CUST-102",
                Name = "Sample Traders",
                Email = "sample@traders.test",
                Phone = "9990002222",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-CUST-103",
                Name = "Mark & Co",
                Email = "mark@company.test",
                Phone = "9990003333",
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    public List<AccountRecord> BuildDummyAccounts(string provider)
    {
        return new List<AccountRecord>
        {
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-ACC-01",
                Name = "Sales",
                Type = "Revenue",
                Code = "4000",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-ACC-02",
                Name = "Accounts Receivable",
                Type = "Asset",
                Code = "1200",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Provider = provider,
                ExternalId = $"{provider}-ACC-03",
                Name = "Bank",
                Type = "Asset",
                Code = "1000",
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}
