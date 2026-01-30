namespace FinSyncNexus.Options;

public class OAuthOptions
{
    public OAuthProviderOptions Xero { get; set; } = new();
    public OAuthProviderOptions Qbo { get; set; } = new();
}

public class OAuthProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
