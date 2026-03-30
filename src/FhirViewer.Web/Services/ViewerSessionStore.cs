using System.Text.Json;
using FhirViewer.Web.Models;

namespace FhirViewer.Web.Services;

public sealed class ViewerSessionStore(IHttpContextAccessor httpContextAccessor)
{
    private const string TokenKey = "fhirviewer.token";
    private const string StateKey = "fhirviewer.oauth.state";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session is available.");

    public OAuthTokenResponse? GetToken()
    {
        var payload = Session.GetString(TokenKey);
        return string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<OAuthTokenResponse>(payload, JsonOptions);
    }

    public void SetToken(OAuthTokenResponse token)
    {
        token.ReceivedAtUtc = DateTimeOffset.UtcNow;
        Session.SetString(TokenKey, JsonSerializer.Serialize(token, JsonOptions));
    }

    public void SetOAuthState(string state) => Session.SetString(StateKey, state);

    public string? GetOAuthState() => Session.GetString(StateKey);

    public void ClearOAuthState() => Session.Remove(StateKey);

    public void ClearAll() => Session.Clear();
}
