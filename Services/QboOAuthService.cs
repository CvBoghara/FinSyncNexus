using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinSyncNexus.Options;
using Microsoft.Extensions.Options;

namespace FinSyncNexus.Services;

public class QboOAuthService
{
    private const string AuthEndpoint = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    private readonly HttpClient _httpClient;
    private readonly OAuthProviderOptions _options;

    public QboOAuthService(HttpClient httpClient, IOptions<OAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value.Qbo;
    }

    public string BuildAuthorizeUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = "com.intuit.quickbooks.accounting",
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state
        };

        var queryString = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));

        return $"{AuthEndpoint}?{queryString}";
    }

    public async Task<(string AccessToken, string RefreshToken)> ExchangeCodeAsync(string code)
    {
        var body = new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        };

        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(body!)
        };

        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var tokenDoc = JsonDocument.Parse(json);

        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        var refreshToken = tokenDoc.RootElement.GetProperty("refresh_token").GetString() ?? string.Empty;

        return (accessToken, refreshToken);
    }
}
