using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to CC98 论坛 (cc98.org).
/// Unlike other services, CC98 uses its own username/password (not ZJUAM).
/// </summary>
public interface ICc98Service : IZjuService;

/// <inheritdoc cref="ICc98Service" />
public sealed class Cc98Service : ICc98Service
{
    private const string TokenUrl = "https://openid.cc98.org/connect/token";

    private readonly string _username;
    private readonly string _password;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    private string _accessToken = "";
    private string _refreshToken = "";
    private string _tokenType = "";
    private DateTime _expiresAt = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of <see cref="Cc98Service"/> with per-user credentials and app-level options.
    /// </summary>
    /// <param name="username">CC98 username.</param>
    /// <param name="password">CC98 password.</param>
    /// <param name="options">App-level CC98 options (ClientId, ClientSecret).</param>
    /// <param name="logger">Logger instance.</param>
    public Cc98Service(string username, string password, Cc98Options options, ILogger<Cc98Service> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientSecret);
        ArgumentNullException.ThrowIfNull(logger);

        _username = username;
        _password = password;
        _clientId = options.ClientId;
        _clientSecret = options.ClientSecret;
        _httpClient = new HttpClient();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[CC98] Login begins.");

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("username", _username),
            new KeyValuePair<string, string>("password", _password),
            new KeyValuePair<string, string>("grant_type", "password")
        ]);

        var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            using var errDoc = JsonDocument.Parse(json);
            var error = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
            var desc = errDoc.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : "";
            throw new LoginException($"[CC98] Login failed: {error} - {desc}");
        }

        ApplyTokenResponse(json);
        _logger.LogInformation("[CC98] Login successful.");
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> FetchAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureLoggedInAsync(cancellationToken);

        // Refresh the token if it's about to expire.
        if (DateTime.UtcNow >= _expiresAt.AddSeconds(-60))
        {
            await RefreshTokenAsync(cancellationToken);
        }

        request.Headers.TryAddWithoutValidation("Authorization", $"{_tokenType} {_accessToken}");

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        _logger.LogInformation("[CC98] Refreshing token...");

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("refresh_token", _refreshToken),
            new KeyValuePair<string, string>("grant_type", "refresh_token")
        ]);

        var response = await _httpClient.PostAsync(TokenUrl, content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            using var errDoc = JsonDocument.Parse(json);
            var error = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
            var desc = errDoc.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : "";
            throw new LoginException($"[CC98] Token refresh failed: {error} - {desc}");
        }

        ApplyTokenResponse(json);
        _logger.LogInformation("[CC98] Token refreshed.");
    }

    private void ApplyTokenResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString() ?? "";
        _refreshToken = root.GetProperty("refresh_token").GetString() ?? "";
        _tokenType = root.GetProperty("token_type").GetString() ?? "Bearer";
        var expiresIn = root.GetProperty("expires_in").GetInt32();
        _expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            return;
        }

        await _loginLock.WaitAsync(ct);
        try
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                await LoginAsync(ct);
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        _loginLock.Dispose();
    }
}
