using FinSyncNexus.Data;
using FinSyncNexus.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using FinSyncNexus.Models;
using FinSyncNexus.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinSyncNexus.Services;

public class SyncService
{
    private readonly AppDbContext _db;
    private readonly DummyDataService _dummyDataService;
    private readonly SyncOptions _syncOptions;
    private readonly HttpClient _httpClient;

    public SyncService(
        AppDbContext db,
        DummyDataService dummyDataService,
        IOptions<SyncOptions> syncOptions,
        HttpClient httpClient)
    {
        _db = db;
        _dummyDataService = dummyDataService;
        _syncOptions = syncOptions.Value;
        _httpClient = httpClient;
    }

    public async Task EnsureDummyDataAsync(string provider)
    {
        var hasInvoices = await _db.Invoices.AnyAsync(i => i.Provider == provider);
        var hasCustomers = await _db.Customers.AnyAsync(c => c.Provider == provider);
        var hasAccounts = await _db.Accounts.AnyAsync(a => a.Provider == provider);

        if (!hasInvoices)
        {
            _db.Invoices.AddRange(_dummyDataService.BuildDummyInvoices(provider));
        }

        if (!hasCustomers)
        {
            _db.Customers.AddRange(_dummyDataService.BuildDummyCustomers(provider));
        }

        if (!hasAccounts)
        {
            _db.Accounts.AddRange(_dummyDataService.BuildDummyAccounts(provider));
        }

        await _db.SaveChangesAsync();
    }

    public bool UseRealApi() => _syncOptions.UseRealApi;

    public async Task SyncProviderAsync(ConnectionStatus connection)
    {
        if (!_syncOptions.UseRealApi)
        {
            await EnsureDummyDataAsync(connection.Provider);
            return;
        }

        if (connection.Provider == "Xero")
        {
            await SyncXeroAsync(connection);
            return;
        }

        if (connection.Provider == "QBO")
        {
            await SyncQboAsync(connection);
            return;
        }
    }

    public async Task MarkSyncedAsync(string provider)
    {
        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Provider == provider);
        if (connection == null)
        {
            return;
        }

        connection.LastSyncAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task SyncXeroAsync(ConnectionStatus connection)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.TenantId))
        {
            await EnsureDummyDataAsync(connection.Provider);
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        _httpClient.DefaultRequestHeaders.Remove("xero-tenant-id");
        _httpClient.DefaultRequestHeaders.Add("xero-tenant-id", connection.TenantId);

        var invoicesJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Invoices");
        var contactsJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Contacts");
        var accountsJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Accounts");

        await UpsertXeroInvoicesAsync(invoicesJson);
        await UpsertXeroCustomersAsync(contactsJson);
        await UpsertXeroAccountsAsync(accountsJson);
    }

    private async Task SyncQboAsync(ConnectionStatus connection)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.RealmId))
        {
            await EnsureDummyDataAsync(connection.Provider);
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var baseUrl = $"https://quickbooks.api.intuit.com/v3/company/{connection.RealmId}/query";
        var invoicesJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Invoice");
        var customersJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Customer");
        var accountsJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Account");

        await UpsertQboInvoicesAsync(invoicesJson);
        await UpsertQboCustomersAsync(customersJson);
        await UpsertQboAccountsAsync(accountsJson);
    }

    private async Task UpsertXeroInvoicesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var invoiceArray = doc.RootElement.GetProperty("Invoices").EnumerateArray();
        await UpsertInvoicesAsync(
            "Xero",
            invoiceArray.Select(item => new InvoiceRecord
            {
                Provider = "Xero",
                ExternalId = item.GetProperty("InvoiceID").GetString() ?? string.Empty,
                InvoiceNumber = item.GetProperty("InvoiceNumber").GetString() ?? string.Empty,
                CustomerName = item.GetProperty("Contact").GetProperty("Name").GetString() ?? string.Empty,
                Amount = item.GetProperty("Total").GetDecimal(),
                Status = item.GetProperty("Status").GetString() ?? string.Empty,
                DueDate = ParseDate(item, "DueDate"),
                CreatedAt = DateTime.UtcNow
            }).ToList()
        );
    }

    private async Task UpsertXeroCustomersAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var contactArray = doc.RootElement.GetProperty("Contacts").EnumerateArray();
        await UpsertCustomersAsync(
            "Xero",
            contactArray.Select(item => new CustomerRecord
            {
                Provider = "Xero",
                ExternalId = item.GetProperty("ContactID").GetString() ?? string.Empty,
                Name = item.GetProperty("Name").GetString() ?? string.Empty,
                Email = item.TryGetProperty("EmailAddress", out var email) ? email.GetString() : null,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        );
    }

    private async Task UpsertXeroAccountsAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var accountArray = doc.RootElement.GetProperty("Accounts").EnumerateArray();
        await UpsertAccountsAsync(
            "Xero",
            accountArray.Select(item => new AccountRecord
            {
                Provider = "Xero",
                ExternalId = item.GetProperty("AccountID").GetString() ?? string.Empty,
                Name = item.GetProperty("Name").GetString() ?? string.Empty,
                Type = item.GetProperty("Type").GetString() ?? string.Empty,
                Code = item.TryGetProperty("Code", out var code) ? code.GetString() : null,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        );
    }

    private async Task UpsertQboInvoicesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
        {
            return;
        }

        if (!response.TryGetProperty("Invoice", out var invoiceArray))
        {
            return;
        }

        await UpsertInvoicesAsync(
            "QBO",
            invoiceArray.EnumerateArray().Select(item => new InvoiceRecord
            {
                Provider = "QBO",
                ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                InvoiceNumber = item.GetProperty("DocNumber").GetString() ?? string.Empty,
                CustomerName = item.GetProperty("CustomerRef").GetProperty("name").GetString() ?? string.Empty,
                Amount = item.GetProperty("TotalAmt").GetDecimal(),
                Status = item.TryGetProperty("Balance", out var balance) && balance.GetDecimal() > 0 ? "OPEN" : "PAID",
                DueDate = ParseDate(item, "DueDate"),
                CreatedAt = DateTime.UtcNow
            }).ToList()
        );
    }

    private async Task UpsertQboCustomersAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
        {
            return;
        }

        if (!response.TryGetProperty("Customer", out var customerArray))
        {
            return;
        }

        await UpsertCustomersAsync(
            "QBO",
            customerArray.EnumerateArray().Select(item => new CustomerRecord
            {
                Provider = "QBO",
                ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                Name = item.GetProperty("DisplayName").GetString() ?? string.Empty,
                Email = item.TryGetProperty("PrimaryEmailAddr", out var emailObj)
                    ? emailObj.GetProperty("Address").GetString()
                    : null,
                Phone = item.TryGetProperty("PrimaryPhone", out var phoneObj)
                    ? phoneObj.GetProperty("FreeFormNumber").GetString()
                    : null,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        );
    }

    private async Task UpsertQboAccountsAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
        {
            return;
        }

        if (!response.TryGetProperty("Account", out var accountArray))
        {
            return;
        }

        await UpsertAccountsAsync(
            "QBO",
            accountArray.EnumerateArray().Select(item => new AccountRecord
            {
                Provider = "QBO",
                ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                Name = item.GetProperty("Name").GetString() ?? string.Empty,
                Type = item.GetProperty("AccountType").GetString() ?? string.Empty,
                Code = item.TryGetProperty("AcctNum", out var code) ? code.GetString() : null,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        );
    }

    private async Task UpsertInvoicesAsync(string provider, List<InvoiceRecord> records)
    {
        var existing = await _db.Invoices.Where(i => i.Provider == provider).ToListAsync();
        var map = existing.ToDictionary(i => i.ExternalId, i => i);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.ExternalId))
            {
                continue;
            }

            if (map.TryGetValue(record.ExternalId, out var current))
            {
                current.InvoiceNumber = record.InvoiceNumber;
                current.CustomerName = record.CustomerName;
                current.Amount = record.Amount;
                current.Status = record.Status;
                current.DueDate = record.DueDate;
            }
            else
            {
                _db.Invoices.Add(record);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertCustomersAsync(string provider, List<CustomerRecord> records)
    {
        var existing = await _db.Customers.Where(c => c.Provider == provider).ToListAsync();
        var map = existing.ToDictionary(c => c.ExternalId, c => c);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.ExternalId))
            {
                continue;
            }

            if (map.TryGetValue(record.ExternalId, out var current))
            {
                current.Name = record.Name;
                current.Email = record.Email;
                current.Phone = record.Phone;
            }
            else
            {
                _db.Customers.Add(record);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertAccountsAsync(string provider, List<AccountRecord> records)
    {
        var existing = await _db.Accounts.Where(a => a.Provider == provider).ToListAsync();
        var map = existing.ToDictionary(a => a.ExternalId, a => a);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.ExternalId))
            {
                continue;
            }

            if (map.TryGetValue(record.ExternalId, out var current))
            {
                current.Name = record.Name;
                current.Type = record.Type;
                current.Code = record.Code;
            }
            else
            {
                _db.Accounts.Add(record);
            }
        }

        await _db.SaveChangesAsync();
    }

    private static DateTime? ParseDate(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(value.GetString(), out var date))
        {
            return date;
        }

        return null;
    }
}
