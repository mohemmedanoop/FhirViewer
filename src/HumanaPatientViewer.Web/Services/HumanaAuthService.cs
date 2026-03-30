using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HumanaPatientViewer.Web.Models;
using HumanaPatientViewer.Web.Options;
using Microsoft.Extensions.Options;

namespace HumanaPatientViewer.Web.Services;

public sealed class HumanaAuthService(HttpClient httpClient, IOptions<HumanaOptions> options)
{
    private readonly HumanaOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string BuildAuthorizationUrl(HttpContext httpContext, string state)
    {
        var redirectUri = BuildRedirectUri(httpContext);
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', _options.Scopes),
            ["state"] = state
        };

        var queryString = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));

        return $"{_options.AuthorityBaseUrl.TrimEnd('/')}/auth/authorize?{queryString}";
    }

    public async Task<HumanaTokenResponse> ExchangeCodeAsync(HttpContext httpContext, string code, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = BuildRedirectUri(httpContext)
        };

        return await SendTokenRequestAsync(form, cancellationToken);
    }

    public async Task<HumanaTokenResponse> RefreshTokenAsync(HttpContext httpContext, string refreshToken, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        return await SendTokenRequestAsync(form, cancellationToken);
    }

    private async Task<HumanaTokenResponse> SendTokenRequestAsync(
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.AuthorityBaseUrl.TrimEnd('/')}/auth/token")
        {
            Content = new FormUrlEncodedContent(form)
        };

        var basicValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicValue);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AddApiKeyIfEnabled(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Humana token request failed ({(int)response.StatusCode}): {payload}");
        }

        var token = JsonSerializer.Deserialize<HumanaTokenResponse>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Humana token response could not be parsed.");

        token.ReceivedAtUtc = DateTimeOffset.UtcNow;
        return token;
    }

    private string BuildRedirectUri(HttpContext httpContext)
    {
        var request = httpContext.Request;
        return $"{request.Scheme}://{request.Host}{_options.RedirectPath}";
    }

    private void AddApiKeyIfEnabled(HttpRequestMessage request)
    {
        if (!_options.UseApiKey || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);
    }
}
