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
    private const int QboPageSize = 1000;
    private const int XeroMaxPages = 200;

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

    public async Task<bool> SyncProviderAsync(ConnectionStatus connection, int userId)
    {
        if (connection.Provider == "Xero")
        {
            var ready = await EnsureValidTokenAsync(connection, userId);
            return ready && await SyncXeroAsync(connection, userId);
        }

        if (connection.Provider == "QBO")
        {
            var ready = await EnsureValidTokenAsync(connection, userId);
            return ready && await SyncQboAsync(connection, userId);
        }

        return false;
    }

    public async Task MarkSyncedAsync(string provider, int userId)
    {
        var connection = await _db.Connections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);
        if (connection == null)
        {
            return;
        }

        connection.LastSyncAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<bool> SyncXeroAsync(ConnectionStatus connection, int userId)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.TenantId))
        {
            await LogErrorAsync(connection.Provider, "SyncXero", "Missing access token or tenant id.", null, userId);
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

            var invoices = await FetchXeroAllAsync(
                resource: "Invoices",
                arrayProperty: "Invoices",
                map: item => new InvoiceRecord
                {
                    Provider = "Xero",
                    UserId = userId,
                    ExternalId = item.GetProperty("InvoiceID").GetString() ?? string.Empty,
                    InvoiceNumber = item.GetProperty("InvoiceNumber").GetString() ?? string.Empty,
                    CustomerName = item.GetProperty("Contact").GetProperty("Name").GetString() ?? string.Empty,
                    Amount = item.GetProperty("Total").GetDecimal(),
                    Status = item.GetProperty("Status").GetString() == "AUTHORISED" ? "OPEN" : (item.GetProperty("Status").GetString() ?? string.Empty),
                    DueDate = ParseDate(item, "DueDate"),
                    CreatedAt = DateTime.UtcNow
                });

            var customers = await FetchXeroAllAsync(
                resource: "Contacts",
                arrayProperty: "Contacts",
                map: item => new CustomerRecord
                {
                    Provider = "Xero",
                    UserId = userId,
                    ExternalId = item.GetProperty("ContactID").GetString() ?? string.Empty,
                    Name = item.GetProperty("Name").GetString() ?? string.Empty,
                    Email = item.TryGetProperty("EmailAddress", out var email) ? email.GetString() : null,
                    CreatedAt = DateTime.UtcNow
                });

            var accounts = await FetchXeroAllAsync(
                resource: "Accounts",
                arrayProperty: "Accounts",
                supportsPaging: false,
                supportsOrdering: false,
                map: item => new AccountRecord
                {
                    Provider = "Xero",
                    UserId = userId,
                    ExternalId = item.GetProperty("AccountID").GetString() ?? string.Empty,
                    Name = item.GetProperty("Name").GetString() ?? string.Empty,
                    Type = item.GetProperty("Type").GetString() ?? string.Empty,
                    Code = item.TryGetProperty("Code", out var code) ? code.GetString() : null,
                    CreatedAt = DateTime.UtcNow
                });

            var payments = await FetchXeroAllAsync(
                resource: "Payments",
                arrayProperty: "Payments",
                supportsPaging: true,
                supportsOrdering: false,
                map: item => new PaymentRecord
                {
                    Provider = "Xero",
                    UserId = userId,
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
                    Method = item.TryGetProperty("PaymentMethod", out var method)
                        ? method.GetString() ?? "Unknown"
                        : "Unknown",
                    Reference = item.TryGetProperty("Reference", out var reference)
                        ? reference.GetString() ?? "-"
                        : "-",
                    CreatedAt = DateTime.UtcNow
                });

            var spendExpenses = await FetchXeroAllAsync(
                resource: "BankTransactions",
                arrayProperty: "BankTransactions",
                supportsOrdering: false,
                map: item =>
                {
                    if (!item.TryGetProperty("Type", out var type) ||
                        type.GetString() is not ("SPEND" or "SPENDCREDIT"))
                    {
                        return null;
                    }

                    return new ExpenseRecord
                    {
                        Provider = "Xero",
                        UserId = userId,
                        ExternalId = item.GetProperty("BankTransactionID").GetString() ?? string.Empty,
                        Date = ParseDate(item, "Date") ?? DateTime.UtcNow,
                        Description = item.TryGetProperty("Reference", out var reference)
                            ? reference.GetString() ?? "Expense"
                            : "Expense",
                        VendorName = item.TryGetProperty("Contact", out var contact)
                            ? contact.GetProperty("Name").GetString() ?? "-"
                            : "-",
                        Category = item.TryGetProperty("BankAccount", out var bankAccount)
                            ? bankAccount.GetProperty("Name").GetString() ?? "-"
                            : "-",
                        Amount = item.TryGetProperty("Total", out var total) ? total.GetDecimal() : 0m,
                        Status = item.TryGetProperty("Status", out var status) ? status.GetString() ?? "Recorded" : "Recorded",
                        CreatedAt = DateTime.UtcNow
                    };
                });

            var billExpenses = await FetchXeroAllAsync(
                resource: "Invoices",
                arrayProperty: "Invoices",
                map: item =>
                {
                    if (!item.TryGetProperty("Type", out var type) || type.GetString() != "ACCPAY")
                    {
                        return null;
                    }

                    return new ExpenseRecord
                    {
                        Provider = "Xero",
                        UserId = userId,
                        ExternalId = TryGetString(item, "InvoiceID", string.Empty),
                        Date = ParseDate(item, "Date") ?? DateTime.UtcNow,
                        Description = TryGetString(item, "Reference", "Bill"),
                        VendorName = item.TryGetProperty("Contact", out var contact)
                            ? contact.GetProperty("Name").GetString() ?? "-"
                            : "-",
                        Category = "Bill",
                        Amount = TryGetDecimal(item, "Total"),
                        Status = TryGetString(item, "Status", "Recorded"),
                        CreatedAt = DateTime.UtcNow
                    };
                });

            var expenses = spendExpenses.Concat(billExpenses).ToList();

            await ReplaceProviderDataAsync(
                provider: "Xero",
                userId: userId,
                invoices: invoices,
                customers: customers,
                accounts: accounts,
                payments: payments,
                expenses: expenses);

            return true;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(connection.Provider, "SyncXero", ex.Message, ex.ToString(), userId);
            return false;
        }
    }

    private async Task<bool> SyncQboAsync(ConnectionStatus connection, int userId)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.RealmId))
        {
            await LogErrorAsync(connection.Provider, "SyncQbo", "Missing access token or realm id.", null, userId);
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
            var invoices = await FetchQboAllAsync(
                baseUrl,
                "Invoice",
                item => new InvoiceRecord
                {
                    Provider = "QBO",
                    UserId = userId,
                    ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                    InvoiceNumber = item.GetProperty("DocNumber").GetString() ?? string.Empty,
                    CustomerName = item.GetProperty("CustomerRef").GetProperty("name").GetString() ?? string.Empty,
                    Amount = item.GetProperty("TotalAmt").GetDecimal(),
                    Status = item.TryGetProperty("Balance", out var balance) && balance.GetDecimal() > 0 ? "OPEN" : "PAID",
                    DueDate = ParseDate(item, "DueDate"),
                    CreatedAt = DateTime.UtcNow
                });

            var customers = await FetchQboAllAsync(
                baseUrl,
                "Customer",
                item => new CustomerRecord
                {
                    Provider = "QBO",
                    UserId = userId,
                    ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                    Name = item.GetProperty("DisplayName").GetString() ?? string.Empty,
                    Email = item.TryGetProperty("PrimaryEmailAddr", out var emailObj)
                        ? emailObj.GetProperty("Address").GetString()
                        : null,
                    Phone = item.TryGetProperty("PrimaryPhone", out var phoneObj)
                        ? phoneObj.GetProperty("FreeFormNumber").GetString()
                        : null,
                    CreatedAt = DateTime.UtcNow
                });

            var accounts = await FetchQboAllAsync(
                baseUrl,
                "Account",
                item => new AccountRecord
                {
                    Provider = "QBO",
                    UserId = userId,
                    ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                    Name = item.GetProperty("Name").GetString() ?? string.Empty,
                    Type = item.GetProperty("AccountType").GetString() ?? string.Empty,
                    Code = item.TryGetProperty("AcctNum", out var code) ? code.GetString() : null,
                    CreatedAt = DateTime.UtcNow
                });

            var payments = await FetchQboAllAsync(
                baseUrl,
                "Payment",
                item => new PaymentRecord
                {
                    Provider = "QBO",
                    UserId = userId,
                    ExternalId = TryGetString(item, "Id", string.Empty),
                    Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                    CustomerName = TryGetRefName(item, "CustomerRef", "-"),
                    InvoiceNumber = TryGetQboInvoiceNumber(item),
                    Amount = TryGetDecimal(item, "TotalAmt"),
                    Method = TryGetRefName(item, "PaymentMethodRef", "Unknown"),
                    Reference = TryGetString(item, "PaymentRefNum", "-"),
                    CreatedAt = DateTime.UtcNow
                });

            var purchases = await FetchQboAllAsync(
                baseUrl,
                "Purchase",
                item => new ExpenseRecord
                {
                    Provider = "QBO",
                    UserId = userId,
                    ExternalId = TryGetString(item, "Id", string.Empty),
                    Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                    Description = TryGetString(item, "PrivateNote", "Expense"),
                    VendorName = TryGetRefName(item, "EntityRef", "-"),
                    Category = TryGetQboExpenseAccount(item),
                    Amount = TryGetDecimal(item, "TotalAmt"),
                    Status = TryGetString(item, "PaymentType", "Recorded"),
                    CreatedAt = DateTime.UtcNow
                });

            var bills = await FetchQboAllAsync(
                baseUrl,
                "Bill",
                item => new ExpenseRecord
                {
                    Provider = "QBO",
                    UserId = userId,
                    ExternalId = TryGetString(item, "Id", string.Empty),
                    Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                    Description = TryGetString(item, "PrivateNote", "Bill"),
                    VendorName = TryGetRefName(item, "VendorRef", "-"),
                    Category = TryGetRefName(item, "APAccountRef", "Bill"),
                    Amount = TryGetDecimal(item, "TotalAmt"),
                    Status = TryGetString(item, "Balance", "Recorded"),
                    CreatedAt = DateTime.UtcNow
                });

            var expenses = purchases.Concat(bills).ToList();

            await ReplaceProviderDataAsync(
                provider: "QBO",
                userId: userId,
                invoices: invoices,
                customers: customers,
                accounts: accounts,
                payments: payments,
                expenses: expenses);

            return true;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(connection.Provider, "SyncQbo", ex.Message, ex.ToString(), userId);
            return false;
        }
    }

    private async Task<bool> EnsureValidTokenAsync(ConnectionStatus connection, int userId)
    {
        if (string.IsNullOrWhiteSpace(connection.AccessToken) || string.IsNullOrWhiteSpace(connection.RefreshToken))
        {
            await LogErrorAsync(connection.Provider, "TokenCheck", "Missing access or refresh token.", null, userId);
            return false;
        }

        var now = DateTime.UtcNow;
        var refreshExpiry = connection.RefreshTokenExpiresAtUtc;
        if (refreshExpiry.HasValue && refreshExpiry.Value <= now.AddMinutes(1))
        {
            await LogErrorAsync(connection.Provider, "TokenCheck", "Refresh token expired. Reconnect required.", null, userId);
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
            await LogErrorAsync(connection.Provider, "TokenRefresh", ex.Message, ex.ToString(), userId);
            return false;
        }
    }

    private async Task UpsertXeroInvoicesAsync(string json, int userId)
    {
        using var doc = JsonDocument.Parse(json);
        var invoiceArray = doc.RootElement.GetProperty("Invoices").EnumerateArray();
        await UpsertInvoicesAsync(
            "Xero",
            userId,
            invoiceArray.Select(item => new InvoiceRecord
            {
                Provider = "Xero",
                UserId = userId,
                ExternalId = item.GetProperty("InvoiceID").GetString() ?? string.Empty,
                InvoiceNumber = item.GetProperty("InvoiceNumber").GetString() ?? string.Empty,
                CustomerName = item.GetProperty("Contact").GetProperty("Name").GetString() ?? string.Empty,
                Amount = item.GetProperty("Total").GetDecimal(),
                Status = item.GetProperty("Status").GetString() == "AUTHORISED" ? "OPEN" : (item.GetProperty("Status").GetString() ?? string.Empty),
                DueDate = ParseDate(item, "DueDate"),
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            pruneMissing: false
        );
    }

    private async Task UpsertXeroCustomersAsync(string json, int userId)
    {
        using var doc = JsonDocument.Parse(json);
        var contactArray = doc.RootElement.GetProperty("Contacts").EnumerateArray();
        await UpsertCustomersAsync(
            "Xero",
            userId,
            contactArray.Select(item => new CustomerRecord
            {
                Provider = "Xero",
                UserId = userId,
                ExternalId = item.GetProperty("ContactID").GetString() ?? string.Empty,
                Name = item.GetProperty("Name").GetString() ?? string.Empty,
                Email = item.TryGetProperty("EmailAddress", out var email) ? email.GetString() : null,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            pruneMissing: false
        );
    }

    private async Task UpsertXeroAccountsAsync(string json, int userId)
    {
        using var doc = JsonDocument.Parse(json);
        var accountArray = doc.RootElement.GetProperty("Accounts").EnumerateArray();
        await UpsertAccountsAsync(
            "Xero",
            userId,
            accountArray.Select(item => new AccountRecord
            {
                Provider = "Xero",
                UserId = userId,
                ExternalId = item.GetProperty("AccountID").GetString() ?? string.Empty,
                Name = item.GetProperty("Name").GetString() ?? string.Empty,
                Type = item.GetProperty("Type").GetString() ?? string.Empty,
                Code = item.TryGetProperty("Code", out var code) ? code.GetString() : null,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            pruneMissing: false
        );
    }

    private async Task UpsertQboInvoicesAsync(string json, int userId)
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
            userId,
            invoiceArray.EnumerateArray().Select(item => new InvoiceRecord
            {
                Provider = "QBO",
                UserId = userId,
                ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                InvoiceNumber = item.GetProperty("DocNumber").GetString() ?? string.Empty,
                CustomerName = item.GetProperty("CustomerRef").GetProperty("name").GetString() ?? string.Empty,
                Amount = item.GetProperty("TotalAmt").GetDecimal(),
                Status = item.TryGetProperty("Balance", out var balance) && balance.GetDecimal() > 0 ? "OPEN" : "PAID",
                DueDate = ParseDate(item, "DueDate"),
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            pruneMissing: false
        );
    }

    private async Task UpsertQboCustomersAsync(string json, int userId)
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
            userId,
            customerArray.EnumerateArray().Select(item => new CustomerRecord
            {
                Provider = "QBO",
                UserId = userId,
                ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                Name = item.GetProperty("DisplayName").GetString() ?? string.Empty,
                Email = item.TryGetProperty("PrimaryEmailAddr", out var emailObj)
                    ? emailObj.GetProperty("Address").GetString()
                    : null,
                Phone = item.TryGetProperty("PrimaryPhone", out var phoneObj)
                    ? phoneObj.GetProperty("FreeFormNumber").GetString()
                    : null,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            pruneMissing: false
        );
    }

    private async Task UpsertQboAccountsAsync(string json, int userId)
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
            userId,
            accountArray.EnumerateArray().Select(item => new AccountRecord
            {
                Provider = "QBO",
                UserId = userId,
                ExternalId = item.GetProperty("Id").GetString() ?? string.Empty,
                Name = item.GetProperty("Name").GetString() ?? string.Empty,
                Type = item.GetProperty("AccountType").GetString() ?? string.Empty,
                Code = item.TryGetProperty("AcctNum", out var code) ? code.GetString() : null,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            pruneMissing: false
        );
    }

    private async Task UpsertXeroPaymentsAsync(string json, int userId)
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
                UserId = userId,
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

        await UpsertPaymentsAsync("Xero", userId, records, pruneMissing: false);
    }

    private async Task UpsertXeroExpensesAsync(string json, int userId)
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
                UserId = userId,
                ExternalId = item.GetProperty("BankTransactionID").GetString() ?? string.Empty,
                Date = ParseDate(item, "Date") ?? DateTime.UtcNow,
                Description = item.TryGetProperty("Reference", out var reference) ? reference.GetString() ?? "Expense" : "Expense",
                VendorName = item.TryGetProperty("Contact", out var contact) ? contact.GetProperty("Name").GetString() ?? "-" : "-",
                Category = item.TryGetProperty("BankAccount", out var bankAccount) ? bankAccount.GetProperty("Name").GetString() ?? "-" : "-",
                Amount = item.TryGetProperty("Total", out var total) ? total.GetDecimal() : 0m,
                Status = item.TryGetProperty("Status", out var status) ? status.GetString() ?? "Recorded" : "Recorded",
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertExpensesAsync("Xero", userId, records, pruneMissing: false);
    }

    private async Task UpsertQboPaymentsAsync(string json, int userId)
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
                UserId = userId,
                ExternalId = TryGetString(item, "Id", string.Empty),
                Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                CustomerName = TryGetRefName(item, "CustomerRef", "-"),
                InvoiceNumber = TryGetQboInvoiceNumber(item),
                Amount = TryGetDecimal(item, "TotalAmt"),
                Method = TryGetRefName(item, "PaymentMethodRef", "Unknown"),
                Reference = TryGetString(item, "PaymentRefNum", "-"),
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertPaymentsAsync("QBO", userId, records, pruneMissing: false);
    }

    private async Task UpsertQboExpensesAsync(string json, int userId)
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
                UserId = userId,
                ExternalId = TryGetString(item, "Id", string.Empty),
                Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                Description = TryGetString(item, "PrivateNote", "Expense"),
                VendorName = TryGetRefName(item, "EntityRef", "-"),
                Category = TryGetQboExpenseAccount(item),
                Amount = TryGetDecimal(item, "TotalAmt"),
                Status = TryGetString(item, "PaymentType", "Recorded"),
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertExpensesAsync("QBO", userId, records, pruneMissing: false);
    }

    private async Task UpsertQboBillsAsync(string json, int userId)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
        {
            return;
        }

        if (!response.TryGetProperty("Bill", out var billArray))
        {
            return;
        }

        var records = billArray.EnumerateArray()
            .Select(item => new ExpenseRecord
            {
                Provider = "QBO",
                UserId = userId,
                ExternalId = TryGetString(item, "Id", string.Empty),
                Date = ParseDate(item, "TxnDate") ?? DateTime.UtcNow,
                Description = TryGetString(item, "PrivateNote", "Bill"),
                VendorName = TryGetRefName(item, "VendorRef", "-"),
                Category = TryGetRefName(item, "APAccountRef", "Bill"),
                Amount = TryGetDecimal(item, "TotalAmt"),
                Status = TryGetString(item, "Balance", "Recorded"),
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertExpensesAsync("QBO", userId, records, pruneMissing: false);
    }

    private async Task UpsertXeroBillsAsync(string json, int userId)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Invoices", out var invoiceArray))
        {
            return;
        }

        var records = invoiceArray.EnumerateArray()
            .Where(item => item.TryGetProperty("Type", out var type) &&
                           type.GetString() == "ACCPAY")
            .Select(item => new ExpenseRecord
            {
                Provider = "Xero",
                UserId = userId,
                ExternalId = TryGetString(item, "InvoiceID", string.Empty),
                Date = ParseDate(item, "Date") ?? DateTime.UtcNow,
                Description = TryGetString(item, "Reference", "Bill"),
                VendorName = item.TryGetProperty("Contact", out var contact)
                    ? contact.GetProperty("Name").GetString() ?? "-"
                    : "-",
                Category = "Bill",
                Amount = TryGetDecimal(item, "Total"),
                Status = TryGetString(item, "Status", "Recorded"),
                CreatedAt = DateTime.UtcNow
            }).ToList();

        await UpsertExpensesAsync("Xero", userId, records, pruneMissing: false);
    }

    private static string BuildXeroUrl(string resource, int page = 1)
    {
        page = page < 1 ? 1 : page;
        return $"https://api.xero.com/api.xro/2.0/{resource}?order=UpdatedDateUTC%20DESC&page={page}";
    }

    private async Task<string> GetQboJsonAsync(string baseUrl, string entity)
    {
        return await GetQboJsonAsync(baseUrl, entity, startPosition: 1, maxResults: QboPageSize);
    }

    private async Task<string> GetQboJsonAsync(string baseUrl, string entity, int startPosition, int maxResults)
    {
        var query =
            $"select * from {entity} orderby MetaData.LastUpdatedTime desc startposition {startPosition} maxresults {maxResults}";
        var url = $"{baseUrl}?query={Uri.EscapeDataString(query)}";
        return await _httpClient.GetStringAsync(url);
    }

    private async Task ReplaceProviderDataAsync(
        string provider,
        int userId,
        List<InvoiceRecord> invoices,
        List<CustomerRecord> customers,
        List<AccountRecord> accounts,
        List<PaymentRecord> payments,
        List<ExpenseRecord> expenses)
    {
        var normalizedInvoices = invoices
            .Where(i => !string.IsNullOrWhiteSpace(i.ExternalId))
            .GroupBy(i => i.ExternalId)
            .Select(g => g.First())
            .ToList();
        var normalizedCustomers = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.ExternalId))
            .GroupBy(c => c.ExternalId)
            .Select(g => g.First())
            .ToList();
        var normalizedAccounts = accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.ExternalId))
            .GroupBy(a => a.ExternalId)
            .Select(g => g.First())
            .ToList();
        var normalizedPayments = payments
            .Where(p => !string.IsNullOrWhiteSpace(p.ExternalId))
            .GroupBy(p => p.ExternalId)
            .Select(g => g.First())
            .ToList();
        var normalizedExpenses = expenses
            .Where(e => !string.IsNullOrWhiteSpace(e.ExternalId))
            .GroupBy(e => e.ExternalId)
            .Select(g => g.First())
            .ToList();

        await using var tx = await _db.Database.BeginTransactionAsync();

        await _db.Invoices
            .Where(i => i.UserId == userId && i.Provider == provider)
            .ExecuteDeleteAsync();
        await _db.Customers
            .Where(c => c.UserId == userId && c.Provider == provider)
            .ExecuteDeleteAsync();
        await _db.Accounts
            .Where(a => a.UserId == userId && a.Provider == provider)
            .ExecuteDeleteAsync();
        await _db.Payments
            .Where(p => p.UserId == userId && p.Provider == provider)
            .ExecuteDeleteAsync();
        await _db.Expenses
            .Where(e => e.UserId == userId && e.Provider == provider)
            .ExecuteDeleteAsync();

        if (normalizedInvoices.Count > 0) await _db.Invoices.AddRangeAsync(normalizedInvoices);
        if (normalizedCustomers.Count > 0) await _db.Customers.AddRangeAsync(normalizedCustomers);
        if (normalizedAccounts.Count > 0) await _db.Accounts.AddRangeAsync(normalizedAccounts);
        if (normalizedPayments.Count > 0) await _db.Payments.AddRangeAsync(normalizedPayments);
        if (normalizedExpenses.Count > 0) await _db.Expenses.AddRangeAsync(normalizedExpenses);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task UpsertPaymentsAsync(string provider, int userId, List<PaymentRecord> records, bool pruneMissing)
    {
        var existing = await _db.Payments
            .Where(p => p.UserId == userId && p.Provider == provider)
            .ToListAsync();
        var map = existing.ToDictionary(p => p.ExternalId, p => p);

        var incomingIds = pruneMissing
            ? records.Select(r => r.ExternalId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet()
            : null;

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

        if (pruneMissing && incomingIds != null)
        {
            var toRemove = existing
                .Where(p => !string.IsNullOrWhiteSpace(p.ExternalId) && !incomingIds.Contains(p.ExternalId))
                .ToList();
            if (toRemove.Count > 0)
            {
                _db.Payments.RemoveRange(toRemove);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertExpensesAsync(string provider, int userId, List<ExpenseRecord> records, bool pruneMissing)
    {
        var existing = await _db.Expenses
            .Where(e => e.UserId == userId && e.Provider == provider)
            .ToListAsync();
        var map = existing.ToDictionary(e => e.ExternalId, e => e);

        var incomingIds = pruneMissing
            ? records.Select(r => r.ExternalId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet()
            : null;

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

        if (pruneMissing && incomingIds != null)
        {
            var toRemove = existing
                .Where(e => !string.IsNullOrWhiteSpace(e.ExternalId) && !incomingIds.Contains(e.ExternalId))
                .ToList();
            if (toRemove.Count > 0)
            {
                _db.Expenses.RemoveRange(toRemove);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertInvoicesAsync(string provider, int userId, List<InvoiceRecord> records, bool pruneMissing)
    {
        var existing = await _db.Invoices
            .Where(i => i.UserId == userId && i.Provider == provider)
            .ToListAsync();
        var map = existing.ToDictionary(i => i.ExternalId, i => i);

        var incomingIds = pruneMissing
            ? records.Select(r => r.ExternalId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet()
            : null;

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

        if (pruneMissing && incomingIds != null)
        {
            var toRemove = existing
                .Where(i => !string.IsNullOrWhiteSpace(i.ExternalId) && !incomingIds.Contains(i.ExternalId))
                .ToList();
            if (toRemove.Count > 0)
            {
                _db.Invoices.RemoveRange(toRemove);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertCustomersAsync(string provider, int userId, List<CustomerRecord> records, bool pruneMissing)
    {
        var existing = await _db.Customers
            .Where(c => c.UserId == userId && c.Provider == provider)
            .ToListAsync();
        var map = existing.ToDictionary(c => c.ExternalId, c => c);

        var incomingIds = pruneMissing
            ? records.Select(r => r.ExternalId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet()
            : null;

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

        if (pruneMissing && incomingIds != null)
        {
            var toRemove = existing
                .Where(c => !string.IsNullOrWhiteSpace(c.ExternalId) && !incomingIds.Contains(c.ExternalId))
                .ToList();
            if (toRemove.Count > 0)
            {
                _db.Customers.RemoveRange(toRemove);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task UpsertAccountsAsync(string provider, int userId, List<AccountRecord> records, bool pruneMissing)
    {
        var existing = await _db.Accounts
            .Where(a => a.UserId == userId && a.Provider == provider)
            .ToListAsync();
        var map = existing.ToDictionary(a => a.ExternalId, a => a);

        var incomingIds = pruneMissing
            ? records.Select(r => r.ExternalId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet()
            : null;

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

        if (pruneMissing && incomingIds != null)
        {
            var toRemove = existing
                .Where(a => !string.IsNullOrWhiteSpace(a.ExternalId) && !incomingIds.Contains(a.ExternalId))
                .ToList();
            if (toRemove.Count > 0)
            {
                _db.Accounts.RemoveRange(toRemove);
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

    private async Task<List<T>> FetchQboAllAsync<T>(
        string baseUrl,
        string entity,
        Func<JsonElement, T?> map) where T : class
    {
        var results = new List<T>();
        var startPosition = 1;

        for (var page = 1; page <= 1000; page++)
        {
            var json = await GetQboJsonAsync(baseUrl, entity, startPosition, QboPageSize);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("QueryResponse", out var response))
            {
                break;
            }

            if (!response.TryGetProperty(entity, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var count = 0;
            foreach (var item in array.EnumerateArray())
            {
                var mapped = map(item);
                if (mapped != null)
                {
                    results.Add(mapped);
                }

                count++;
            }

            if (count <= 0)
            {
                break;
            }

            startPosition += count;
            if (count < QboPageSize)
            {
                break;
            }
        }

        return results;
    }

    private async Task<List<T>> FetchXeroAllAsync<T>(
        string resource,
        string arrayProperty,
        Func<JsonElement, T?> map,
        bool supportsPaging = true,
        bool supportsOrdering = true) where T : class
    {
        var results = new List<T>();
        var maxPages = supportsPaging ? XeroMaxPages : 1;

        for (var page = 1; page <= maxPages; page++)
        {
            var url = BuildXeroUrl(resource, page, supportsPaging, supportsOrdering);
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Xero API error ({resource}): {response.StatusCode} - {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(arrayProperty, out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var count = 0;
            foreach (var item in array.EnumerateArray())
            {
                var mapped = map(item);
                if (mapped != null)
                {
                    results.Add(mapped);
                }

                count++;
            }

            if (count <= 0 || !supportsPaging)
            {
                break;
            }
        }

        return results;
    }

    private static string BuildXeroUrl(string resource, int page, bool paged, bool ordered)
    {
        var url = $"https://api.xero.com/api.xro/2.0/{resource}";
        var queryParams = new List<string>();

        if (ordered)
        {
            queryParams.Add("order=UpdatedDateUTC%20DESC");
        }

        if (paged)
        {
            var p = page < 1 ? 1 : page;
            queryParams.Add($"page={p}");
        }

        if (queryParams.Any())
        {
            url += "?" + string.Join("&", queryParams);
        }

        return url;
    }

    private async Task LogErrorAsync(string provider, string context, string message, string? details, int userId)
    {
        _db.SyncErrors.Add(new SyncErrorLog
        {
            UserId = userId,
            Provider = provider,
            Context = context,
            Message = message,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
