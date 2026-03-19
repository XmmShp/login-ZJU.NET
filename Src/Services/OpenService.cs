using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to 浙大先生开放平台 / HiAgent (open.zju.edu.cn).
/// </summary>
public interface IOpenService : IZjuService;

/// <inheritdoc cref="IOpenService" />
public sealed class OpenService : ZjuServiceBase, IOpenService
{
    private static readonly Dictionary<string, string> SaferHeaders = new()
    {
        ["accept"] = "application/json, text/plain, */*",
        ["accept-language"] = "en",
        ["cache-control"] = "no-cache",
        ["content-type"] = "application/json",
        ["pragma"] = "no-cache",
        ["sec-ch-ua"] = "\"Chromium\";v=\"142\", \"Microsoft Edge\";v=\"142\", \"Not_A Brand\";v=\"99\"",
        ["sec-ch-ua-mobile"] = "?0",
        ["sec-ch-ua-platform"] = "\"Windows\"",
        ["sec-fetch-dest"] = "empty",
        ["sec-fetch-mode"] = "cors",
        ["sec-fetch-site"] = "same-origin",
        ["x-top-region"] = "cn-north-1",
        ["Referer"] = "https://open.zju.edu.cn/",
    };

    private string _xcsrfToken = "";

    /// <summary>
    /// Initializes a new instance of <see cref="OpenService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenService(IZjuamAuth auth, ILogger<OpenService> logger)
        : base(auth, logger) { }

    /// <inheritdoc />
    public override async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[OPEN] Login begins.");

        // Step 1: Request the login API to get the OAuth2 redirect URI.
        var payload = JsonSerializer.Serialize(new { SSO = "Oauth", IdpID = "2", RedirectUrl = "/" });
        var prepareRequest = new HttpRequestMessage(HttpMethod.Post, "https://open.zju.edu.cn/api/auth/login")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        foreach (var (key, value) in SaferHeaders)
        {
            prepareRequest.Headers.TryAddWithoutValidation(key, value);
        }

        var prepareResponse = await Http.SendAsync(prepareRequest, cancellationToken);
        var prepareJson = await prepareResponse.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(prepareJson);
        if (!doc.RootElement.TryGetProperty("redirect_uri", out var redirectUriProp))
        {
            throw new LoginException($"[OPEN] Failed to get redirect_uri: {prepareJson}");
        }

        var redirectUri = redirectUriProp.GetString()
            ?? throw new LoginException("[OPEN] redirect_uri is null.");

        Logger.LogDebug("[OPEN] Redirecting to ZJUAM: {Url}", redirectUri);

        // Step 2: Authenticate via ZJUAM OAuth2.
        var callbackUrl = await Auth.LoginServiceOAuth2Async(redirectUri, cancellationToken);
        Logger.LogDebug("[OPEN] Returned from ZJUAM: {Url}", callbackUrl);

        // Step 3: Follow the callback redirect.
        var callbackResponse = await Http.GetAsync(callbackUrl, cancellationToken);
        var finalLocation = callbackResponse.Headers.Location?.ToString();
        if (finalLocation is not null)
        {
            var finalUrl = new Uri(new Uri("https://open.zju.edu.cn"), finalLocation).ToString();
            await Http.GetAsync(finalUrl, cancellationToken);
        }

        // Step 4: Extract x-csrf-token from cookies.
        var cookies = Http.Cookies.GetCookies(new Uri("https://open.zju.edu.cn/"));
        _xcsrfToken = cookies["x-csrf-token"]?.Value ?? "";

        Logger.LogInformation("[OPEN] Login finalized.");
    }

    /// <inheritdoc />
    public override async Task<HttpResponseMessage> FetchAsync(
        string url,
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Apply the required headers for open.zju.edu.cn.
        foreach (var (key, value) in SaferHeaders)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        request.Headers.TryAddWithoutValidation("x-csrf-token", _xcsrfToken);

        configureRequest?.Invoke(request);

        return await Http.SendFollowingRedirectsAsync(request, cancellationToken);
    }
}
