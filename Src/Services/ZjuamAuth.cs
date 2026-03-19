using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace LoginZju;

/// <summary>
/// Provides authentication with the ZJU unified identity platform (zjuam.zju.edu.cn).
/// This is the core authentication class required by most other ZJU services.
/// </summary>
public sealed partial class ZjuamAuth : IZjuamAuth
{
    private const string CasLoginUrl = "https://zjuam.zju.edu.cn/cas/login";
    private const string PubKeyUrl = "https://zjuam.zju.edu.cn/cas/v2/getPubKey";
    private const string ZjuamHost = "zjuam.zju.edu.cn";

    private readonly string _username;
    private readonly string _password;
    private readonly CookieHttpClient _http;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _loggedIn;

    /// <summary>
    /// Initializes a new instance of <see cref="ZjuamAuth"/> with the specified credentials.
    /// </summary>
    /// <param name="username">ZJU unified identity username.</param>
    /// <param name="password">ZJU unified identity password.</param>
    /// <param name="logger">Logger instance.</param>
    public ZjuamAuth(string username, string password, ILogger<ZjuamAuth> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        _username = username;
        _password = password;
        _http = new CookieHttpClient();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LoginAsync(CancellationToken cancellationToken = default)
        => await EnsureLoggedInAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<HttpResponseMessage> FetchAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureLoggedInAsync(cancellationToken);
        return await _http.SendFollowingRedirectsAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> LoginServiceAsync(string serviceUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[ZJUAM] Attempting to login to service: {ServiceUrl}", serviceUrl);

        var fullLoginUrl = $"{CasLoginUrl}?service={Uri.EscapeDataString(serviceUrl)}";

        if (_loggedIn)
        {
            var response = await _http.GetAsync(fullLoginUrl, cancellationToken);
            _logger.LogDebug("[ZJUAM] loginSvc response: {Status} {Location}",
                (int)response.StatusCode, response.Headers.Location);

            if (response.StatusCode == HttpStatusCode.Found)
            {
                return response.Headers.Location!.ToString();
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await LoginCoreAsync(fullLoginUrl, cancellationToken);
            }

            throw new LoginException(
                $"Login to service failed with status {(int)response.StatusCode}.");
        }

        return await LoginCoreAsync(fullLoginUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> LoginServiceOAuth2Async(
        string redirectUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[ZJUAM] Attempting OAuth2 login: {RedirectUrl}", redirectUrl);
        await EnsureLoggedInAsync(cancellationToken);

        var currentUrl = redirectUrl;
        while (new Uri(currentUrl).Host.Equals(ZjuamHost, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[ZJUAM] OAuth2 redirect: {Url}", currentUrl);
            var response = await _http.GetAsync(currentUrl, cancellationToken);
            currentUrl = response.Headers.Location?.ToString()
                ?? throw new LoginException("No redirect location found during OAuth2 login.");
        }

        return currentUrl;
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (_loggedIn)
        {
            return;
        }

        await _loginLock.WaitAsync(ct);
        try
        {
            if (!_loggedIn)
            {
                await LoginCoreAsync(CasLoginUrl, ct);
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<string> LoginCoreAsync(string loginUrl, CancellationToken ct)
    {
        _logger.LogInformation("[ZJUAM] Logging in...");

        // Step 1: GET login page to obtain the execution token and populate cookies.
        // Must follow redirects here (the CAS server may redirect before serving the login form).
        var pageResponse = await _http.SendFollowingRedirectsAsync(
            new HttpRequestMessage(HttpMethod.Get, loginUrl), ct);
        var html = await pageResponse.Content.ReadAsStringAsync(ct);

        var executionMatch = ExecutionPattern().Match(html);
        if (!executionMatch.Success)
        {
            throw new LoginException("Login page does not contain an execution token.");
        }

        var execution = executionMatch.Groups[1].Value;

        // Step 2: Fetch the RSA public key.
        var pubKeyResponse = await _http.GetAsync(PubKeyUrl, ct);
        var pubKeyJson = await pubKeyResponse.Content.ReadAsStringAsync(ct);

        var modulusMatch = ModulusPattern().Match(pubKeyJson);
        var exponentMatch = ExponentPattern().Match(pubKeyJson);
        if (!modulusMatch.Success || !exponentMatch.Success)
        {
            throw new LoginException("Failed to parse RSA public key from ZJUAM.");
        }

        var encryptedPassword = RsaHelper.Encrypt(
            _password, exponentMatch.Groups[1].Value, modulusMatch.Groups[1].Value);

        // Step 3: POST the login form.
        var formContent = new FormUrlEncodedContent(new KeyValuePair<string, string>[]
        {
            new("username", _username),
            new("password", encryptedPassword),
            new("execution", execution),
            new("_eventId", "submit"),
            new("authcode", ""),
        });

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, loginUrl)
        {
            Content = formContent,
        };

        var loginResponse = await _http.SendAsync(loginRequest, ct);

        if (loginResponse.StatusCode == HttpStatusCode.Found)
        {
            _loggedIn = true;
            _logger.LogInformation("[ZJUAM] Login successful.");
            return loginResponse.Headers.Location!.ToString();
        }

        if (loginResponse.StatusCode == HttpStatusCode.OK)
        {
            var responseHtml = await loginResponse.Content.ReadAsStringAsync(ct);
            var msgMatch = ErrorMsgPattern().Match(responseHtml);
            var message = msgMatch.Success ? msgMatch.Groups[1].Value : "Unknown error";
            throw new LoginException($"Login failed: {message}");
        }

        throw new LoginException($"Login failed with status {(int)loginResponse.StatusCode}.");
    }

    [GeneratedRegex("name=\"execution\" value=\"([^\"]+)\"")]
    private static partial Regex ExecutionPattern();

    [GeneratedRegex("\"modulus\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex ModulusPattern();

    [GeneratedRegex("\"exponent\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex ExponentPattern();

    [GeneratedRegex("<span id=\"msg\">([^<]+)</span>")]
    private static partial Regex ErrorMsgPattern();

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _loginLock.Dispose();
    }
}
