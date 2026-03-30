using System.Net.Http.Headers;
using FhirViewer.Web.Options;
using Microsoft.Extensions.Options;

namespace FhirViewer.Web.Services;

public sealed class FhirApiService(HttpClient httpClient, IOptions<FhirConnectionOptions> options)
{
    private readonly FhirConnectionOptions _options = options.Value;

    public async Task<string> GetBundleAsync(string resourceType, string accessToken, CancellationToken cancellationToken)
    {
        var requestUri = $"{_options.FhirBaseUrl.TrimEnd('/')}/{resourceType}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        AddApiKeyIfEnabled(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"FHIR request for {resourceType} failed ({(int)response.StatusCode}): {payload}");
        }

        return payload;
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
