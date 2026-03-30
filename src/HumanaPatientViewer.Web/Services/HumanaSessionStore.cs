using System.Text.Json;
using HumanaPatientViewer.Web.Models;

namespace HumanaPatientViewer.Web.Services;

public sealed class HumanaSessionStore(IHttpContextAccessor httpContextAccessor)
{
    private const string TokenKey = "humana.token";
    private const string StateKey = "humana.oauth.state";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session is available.");

    public HumanaTokenResponse? GetToken()
    {
        var payload = Session.GetString(TokenKey);
        return string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<HumanaTokenResponse>(payload, JsonOptions);
    }

    public void SetToken(HumanaTokenResponse token)
    {
        token.ReceivedAtUtc = DateTimeOffset.UtcNow;
        Session.SetString(TokenKey, JsonSerializer.Serialize(token, JsonOptions));
    }

    public void SetOAuthState(string state) => Session.SetString(StateKey, state);

    public string? GetOAuthState() => Session.GetString(StateKey);

    public void ClearOAuthState() => Session.Remove(StateKey);

    public void ClearAll() => Session.Clear();
}
