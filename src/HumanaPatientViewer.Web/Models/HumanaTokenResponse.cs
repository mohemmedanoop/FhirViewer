using System.Text.Json.Serialization;

namespace HumanaPatientViewer.Web.Models;

public sealed class HumanaTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("patient")]
    public string? Patient { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAtUtc => ReceivedAtUtc.AddSeconds(Math.Max(0, ExpiresIn));
}
