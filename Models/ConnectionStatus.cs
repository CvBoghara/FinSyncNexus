namespace FinSyncNexus.Models;

public class ConnectionStatus
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty; // "Xero" or "QBO"
    public bool IsConnected { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    public string? RealmId { get; set; } // QBO company id
    public string? TenantId { get; set; } // Xero tenant id
}
