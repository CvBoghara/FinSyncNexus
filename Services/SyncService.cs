using FinSyncNexus.Data;
using FinSyncNexus.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using FinSyncNexus.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinSyncNexus.Services;

public class SyncService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly XeroOAuthService _xeroOAuthService;
    private readonly QboOAuthService _qboOAuthService;
    private readonly string _qboApiBaseUrl;

    public SyncService(
        AppDbContext db,
        HttpClient httpClient,
        XeroOAuthService xeroOAuthService,
        QboOAuthService qboOAuthService,
        IOptions<OAuthOptions> options)
    {
        _db = db;
        _httpClient = httpClient;
        _xeroOAuthService = xeroOAuthService;
        _qboOAuthService = qboOAuthService;
        var configuredBase = options.Value.Qbo.ApiBaseUrl?.Trim();
        _qboApiBaseUrl = string.IsNullOrWhiteSpace(configuredBase)
            ? "https://quickbooks.api.intuit.com"
            : configuredBase.TrimEnd('/');
    }

    public async Task<bool> SyncProviderAsync(ConnectionStatus connection)
    {
        if (connection.Provider == "Xero")
        {
            var ready = await EnsureValidTokenAsync(connection);
            return ready && await SyncXeroAsync(connection);
        }

        if (connection.Provider == "QBO")
        {
            var ready = await EnsureValidTokenAsync(connection);
            return ready && await SyncQboAsync(connection);
        }

        return false;
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

    private async Task<bool> SyncXeroAsync(ConnectionStatus connection)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.TenantId))
        {
            await LogErrorAsync(connection.Provider, "SyncXero", "Missing access token or tenant id.", null);
            return false;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", connection.AccessToken);
            _httpClient.DefaultRequestHeaders.Remove("xero-tenant-id");
            _httpClient.DefaultRequestHeaders.Add("xero-tenant-id", connection.TenantId);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var invoicesJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Invoices");
            var contactsJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Contacts");
            var accountsJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Accounts");
            var paymentsJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/Payments");
            var transactionsJson = await _httpClient.GetStringAsync("https://api.xero.com/api.xro/2.0/BankTransactions");

            await UpsertXeroInvoicesAsync(invoicesJson);
            await UpsertXeroCustomersAsync(contactsJson);
            await UpsertXeroAccountsAsync(accountsJson);
            await UpsertXeroPaymentsAsync(paymentsJson);
            await UpsertXeroExpensesAsync(transactionsJson);

            return true;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(connection.Provider, "SyncXero", ex.Message, ex.ToString());
            return false;
        }
    }

    private async Task<bool> SyncQboAsync(ConnectionStatus connection)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.RealmId))
        {
            await LogErrorAsync(connection.Provider, "SyncQbo", "Missing access token or realm id.", null);
            return false;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", connection.AccessToken);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Remove("xero-tenant-id");

            var baseUrl = $"{_qboApiBaseUrl}/v3/company/{connection.RealmId}/query";
            var invoicesJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Invoice");
            var customersJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Customer");
            var accountsJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Account");
            var paymentsJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Payment");
            var expensesJson = await _httpClient.GetStringAsync($"{baseUrl}?query=select%20*%20from%20Purchase");

            await UpsertQboInvoicesAsync(invoicesJson);
            await UpsertQboCustomersAsync(customersJson);
            await UpsertQboAccountsAsync(accountsJson);
            await UpsertQboPaymentsAsync(paymentsJson);
            await UpsertQboExpensesAsync(expensesJson);

            return true;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(connection.Provider, "SyncQbo", ex.Message, ex.ToString());
            return false;
        }
    }

    private async Task<bool> EnsureValidTokenAsync(ConnectionStatus connection)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.RefreshToken))
        {
            await LogErrorAsync(connection.Provider, "TokenCheck", "Missing access or refresh token.", null);
            return false;
        }

        var now = DateTime.UtcNow;
        var refreshExpiry = connection.RefreshTokenExpiresAtUtc;
        if (refreshExpiry.HasValue && refreshExpiry.Value <= now.AddMinutes(1))
        {
            await LogErrorAsync(connection.Provider, "TokenCheck", "Refresh token expired. Reconnect required.", null);
            return false;
        }

        var needsRefresh = !connection.AccessTokenExpiresAtUtc.HasValue ||
                           connection.AccessTokenExpiresAtUtc.Value <= now.AddMinutes(2);

        if (!needsRefresh)
        {
            return true;
        }

        try
        {
            if (connection.Provider == "Xero")
            {
                var token = await _xeroOAuthService.RefreshAccessTokenAsync(connection.RefreshToken);
                connection.AccessToken = token.AccessToken;
                connection.RefreshToken = token.RefreshToken;
                connection.AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc;
                connection.RefreshTokenExpiresAtUtc = token.RefreshTokenExpiresAtUtc ?? connection.RefreshTokenExpiresAtUtc;
            }
            else if (connection.Provider == "QBO")
            {
                var token = await _qboOAuthService.RefreshAccessTokenAsync(connection.RefreshToken);
                connection.AccessToken = token.AccessToken;
                connection.RefreshToken = token.RefreshToken;
                connection.AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc;
                connection.RefreshTokenExpiresAtUtc = token.RefreshTokenExpiresAtUtc ?? connection.RefreshTokenExpiresAtUtc;
            }

            await _db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(connection.Provider, "TokenRefresh", ex.Message, ex.ToString());
            return false;
        }
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

    private async Task UpsertXeroPaymentsAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Payments", out var paymentArray))
        {
            return;
        }

        var records = paymentArray.EnumerateArray()
            .Select(item => new PaymentRecord
            {
                Provider = "Xero",
                ExternalId = item.GetProperty("PaymentID").GetString() ?? string.Empty,
                Date = ParseDate(item, "Date") ?? DateTime.UtcNow,
                CustomerName = item.TryGetProperty("Invoice", out var invoice)
                    && invoice.TryGetProperty("Contact", out var contact)
                        ? contact.GetProperty("Name").GetString() ?? "-"
                        : "-",
                InvoiceNumber = item.TryGetProperty("Invoice", out var inv)
                    ? inv.GetProperty("InvoiceNumber").GetString() ?? "-"
                    : "-",
                Amount = item.TryGetProperty("Amount", out var amount) ? amount.GetDecimal() : 0m,
                Method = item.TryGetProperty("PaymentMethod", out var method) ? method.GetString() ?? "Unknown" : "Unknown",
                Reference = item.TryGetProperty("Reference", out var reference) ? reference.GetString() ?? "-" : "-",
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertPaymentsAsync("Xero", records);
    }

    private async Task UpsertXeroExpensesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("BankTransactions", out var transactionArray))
        {
            return;
        }

        var records = transactionArray.EnumerateArray()
            .Where(item => item.TryGetProperty("Type", out var type) &&
                           type.GetString() is "SPEND" or "SPENDCREDIT")
            .Select(item => new ExpenseRecord
            {
                Provider = "Xero",
                ExternalId = item.GetProperty("BankTransactionID").GetString() ?? string.Empty,
                Date = ParseDate(item, "Date") ?? DateTime.UtcNow,
                Description = item.TryGetProperty("Reference", out var reference) ? reference.GetString() ?? "Expense" : "Expense",
                VendorName = item.TryGetProperty("Contact", out var contact) ? contact.GetProperty("Name").GetString() ?? "-" : "-",
                Category = item.TryGetProperty("BankAccount", out var bankAccount) ? bankAccount.GetProperty("Name").GetString() ?? "-" : "-",
                Amount = item.TryGetProperty("Total", out var total) ? total.GetDecimal() : 0m,
                Status = item.TryGetProperty("Status", out var status) ? status.GetString() ?? "Recorded" : "Recorded",
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertExpensesAsync("Xero", records);
    }

    private async Task UpsertQboPaymentsAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
        {
            return;
        }

        if (!response.TryGetProperty("Payment", out var paymentArray))
        {
            return;
        }

        var records = paymentArray.EnumerateArray()
            .Select(item => new PaymentRecord
            {
                Provider = "QBO",
                ExternalId = TryGetString(item, "Id", string.Empty),
                Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                CustomerName = TryGetRefName(item, "CustomerRef", "-"),
                InvoiceNumber = TryGetQboInvoiceNumber(item),
                Amount = TryGetDecimal(item, "TotalAmt"),
                Method = TryGetRefName(item, "PaymentMethodRef", "Unknown"),
                Reference = TryGetString(item, "PaymentRefNum", "-"),
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertPaymentsAsync("QBO", records);
    }

    private async Task UpsertQboExpensesAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
        {
            return;
        }

        if (!response.TryGetProperty("Purchase", out var purchaseArray))
        {
            return;
        }

        var records = purchaseArray.EnumerateArray()
            .Select(item => new ExpenseRecord
            {
                Provider = "QBO",
                ExternalId = TryGetString(item, "Id", string.Empty),
                Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                Description = TryGetString(item, "PrivateNote", "Expense"),
                VendorName = TryGetRefName(item, "EntityRef", "-"),
                Category = TryGetQboExpenseAccount(item),
                Amount = TryGetDecimal(item, "TotalAmt"),
                Status = TryGetString(item, "PaymentType", "Recorded"),
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertExpensesAsync("QBO", records);
    }

    private async Task UpsertPaymentsAsync(string provider, List<PaymentRecord> records)
    {
        var existing = await _db.Payments.Where(p => p.Provider == provider).ToListAsync();
        var map = existing.ToDictionary(p => p.ExternalId, p => p);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.ExternalId))
            {
                continue;
            }

            if (map.TryGetValue(record.ExternalId, out var current))
            {
                current.Date = record.Date;
                current.CustomerName = record.CustomerName;
                current.InvoiceNumber = record.InvoiceNumber;
                current.Amount = record.Amount;
                current.Method = record.Method;
                current.Reference = record.Reference;
            }
            else
            {
                _db.Payments.Add(record);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertExpensesAsync(string provider, List<ExpenseRecord> records)
    {
        var existing = await _db.Expenses.Where(e => e.Provider == provider).ToListAsync();
        var map = existing.ToDictionary(e => e.ExternalId, e => e);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.ExternalId))
            {
                continue;
            }

            if (map.TryGetValue(record.ExternalId, out var current))
            {
                current.Date = record.Date;
                current.Description = record.Description;
                current.VendorName = record.VendorName;
                current.Category = record.Category;
                current.Amount = record.Amount;
                current.Status = record.Status;
            }
            else
            {
                _db.Expenses.Add(record);
            }
        }

        await _db.SaveChangesAsync();
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

    private static string TryGetQboInvoiceNumber(JsonElement item)
    {
        if (!item.TryGetProperty("Line", out var lines))
        {
            return "-";
        }

        foreach (var line in lines.EnumerateArray())
        {
            if (!line.TryGetProperty("LinkedTxn", out var linked))
            {
                continue;
            }

            foreach (var txn in linked.EnumerateArray())
            {
                if (txn.TryGetProperty("TxnId", out var txnId))
                {
                    return txnId.GetString() ?? "-";
                }
            }
        }

        return "-";
    }

    private static string TryGetQboExpenseAccount(JsonElement item)
    {
        if (!item.TryGetProperty("Line", out var lines))
        {
            return "-";
        }

        foreach (var line in lines.EnumerateArray())
        {
            if (!line.TryGetProperty("AccountBasedExpenseLineDetail", out var detail))
            {
                continue;
            }

            if (detail.TryGetProperty("AccountRef", out var accountRef) &&
                accountRef.TryGetProperty("name", out var name))
            {
                return name.GetString() ?? "-";
            }
        }

        return "-";
    }

    private static string TryGetString(JsonElement item, string property, string fallback)
    {
        if (item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? fallback;
        }

        return fallback;
    }

    private static decimal TryGetDecimal(JsonElement item, string property)
    {
        if (item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDecimal();
        }

        return 0m;
    }

    private static string TryGetRefName(JsonElement item, string property, string fallback)
    {
        if (item.TryGetProperty(property, out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            if (obj.TryGetProperty("name", out var name))
            {
                return name.GetString() ?? fallback;
            }
        }

        return fallback;
    }

    private async Task LogErrorAsync(string provider, string context, string message, string? details)
    {
        _db.SyncErrors.Add(new SyncErrorLog
        {
            Provider = provider,
            Context = context,
            Message = message,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
