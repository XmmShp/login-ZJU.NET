using System.Net;
using System.Text.RegularExpressions;

namespace LoginZju;

/// <summary>
/// A lightweight HTTP client wrapper that manages cookies via <see cref="CookieContainer"/>
/// and provides manual redirect-following helpers for authentication flows.
/// </summary>
internal sealed partial class CookieHttpClient : IDisposable
{
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0";

    private readonly HttpClient _client;

    public CookieContainer Cookies { get; }

    public CookieHttpClient()
    {
        Cookies = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = Cookies,
            AllowAutoRedirect = false,
            UseCookies = true,
        };
        _client = new HttpClient(handler);
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
    }

    /// <summary>
    /// Sends a request without following redirects.
    /// </summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
        => _client.SendAsync(request, ct);

    /// <summary>
    /// Sends a GET request without following redirects.
    /// </summary>
    public Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct = default)
        => SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);

    /// <summary>
    /// Sends a request and automatically follows HTTP redirects (3xx).
    /// </summary>
    public async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
        HttpRequestMessage request, CancellationToken ct = default)
    {
        var response = await _client.SendAsync(request, ct);
        while (IsRedirect(response.StatusCode))
        {
            var location = ResolveLocation(request.RequestUri!, response);
            if (location is null)
            {
                break;
            }
            request = new HttpRequestMessage(HttpMethod.Get, location);
            response = await _client.SendAsync(request, ct);
        }
        return response;
    }

    /// <summary>
    /// Follows redirect chain from <paramref name="startUrl"/> until the response host
    /// matches <paramref name="targetHost"/>. Returns the URL that points to the target host.
    /// </summary>
    public async Task<string> FollowRedirectsUntilHostAsync(
        string startUrl, string targetHost, CancellationToken ct = default)
    {
        var currentUrl = startUrl;
        while (!new Uri(currentUrl).Host.Equals(targetHost, StringComparison.OrdinalIgnoreCase))
        {
            var response = await GetAsync(currentUrl, ct);
            var location = ResolveLocation(new Uri(currentUrl), response);
            currentUrl = location?.ToString()
                ?? throw new LoginException($"No redirect location found at {currentUrl}");
        }
        return currentUrl;
    }

    /// <summary>
    /// Follows all redirects (HTTP 3xx and HTML meta-refresh) from the given URL until a
    /// non-redirect response is reached. Returns the final response.
    /// </summary>
    public async Task<HttpResponseMessage> FollowAllRedirectsAsync(string startUrl, CancellationToken ct = default)
    {
        var currentUrl = startUrl;
        while (true)
        {
            var response = await GetAsync(currentUrl, ct);

            if (IsRedirect(response.StatusCode))
            {
                var location = ResolveLocation(new Uri(currentUrl), response);
                if (location is null)
                {
                    return response;
                }
                currentUrl = location.ToString();
                continue;
            }

            // Check for HTML meta-refresh redirect
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var match = MetaRefreshPattern().Match(content);
                if (match.Success)
                {
                    currentUrl = match.Groups[1].Value;
                    continue;
                }
            }

            return response;
        }
    }

    private static Uri? ResolveLocation(Uri requestUri, HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is null)
        {
            return null;
        }

        return location.IsAbsoluteUri ? location : new Uri(requestUri, location);
    }

    private static bool IsRedirect(HttpStatusCode status)
        => (int)status >= 300 && (int)status < 400;

    [GeneratedRegex("meta http-equiv=\"refresh\" content=\"0;URL=([^\"]+)\"")]
    private static partial Regex MetaRefreshPattern();

    public void Dispose() => _client.Dispose();
}
