using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to 表单填报助手 (form.zju.edu.cn).
/// Uses token-based authentication rather than cookies.
/// </summary>
public interface IFormService : IZjuService;

/// <inheritdoc cref="IFormService" />
public sealed class FormService : IFormService
{
    private const string ServiceUrl = "https://form.zju.edu.cn/";
    private static readonly byte[] AesKey = Convert.FromHexString("74102f635c6d4b22b270239bc1e84f50");

    private readonly IZjuamAuth _auth;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private string _token = "";

    /// <summary>
    /// Initializes a new instance of <see cref="FormService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Logger instance.</param>
    public FormService(IZjuamAuth auth, ILogger<FormService> logger)
    {
        _auth = auth;
        _httpClient = new HttpClient();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[FORM] Login begins.");

        var callbackUrl = await _auth.LoginServiceAsync(ServiceUrl, cancellationToken).ConfigureAwait(false);
        var ticket = ExtractTicket(callbackUrl);
        var encodedTicket = EncryptTicket(ticket);

        var validateUrl =
            $"https://form.zju.edu.cn/dfi/validateLogin?ticket={encodedTicket}&service={Uri.EscapeDataString(ServiceUrl)}";

        var response = await _httpClient.GetAsync(validateUrl, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("code", out var code) && code.GetInt32() == 2000)
        {
            _token = root.GetProperty("data").GetProperty("token").GetString()
                ?? throw new LoginException("[FORM] Token not found in response.");
            _logger.LogInformation("[FORM] Login successful.");
        }
        else
        {
            throw new LoginException($"[FORM] Login failed: {json}");
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> FetchAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureLoggedInAsync(cancellationToken).ConfigureAwait(false);

        using var firstRequest = await HttpRequestMessageHelper.CloneAsync(request, cancellationToken).ConfigureAwait(false);
        firstRequest.Headers.TryAddWithoutValidation("authentication", _token);

        var response = await _httpClient.SendAsync(firstRequest, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("[FORM] Token expired, re-authenticating...");
            _token = "";
            await LoginAsync(cancellationToken).ConfigureAwait(false);

            using var retryRequest = await HttpRequestMessageHelper.CloneAsync(request, cancellationToken).ConfigureAwait(false);
            retryRequest.Headers.TryAddWithoutValidation("authentication", _token);
            response = await _httpClient.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            return;
        }

        await _loginLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrEmpty(_token))
            {
                await LoginAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private static string ExtractTicket(string callbackUrl)
    {
        var uri = new Uri(callbackUrl);
        var query = uri.Query.TrimStart('?');
        foreach (var param in query.Split('&'))
        {
            var parts = param.Split('=', 2);
            if (parts[0] == "ticket")
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new LoginException("[FORM] No ticket found in callback URL.");
    }

    /// <summary>
    /// Encrypts a ticket using AES-128-ECB then double-Base64 encodes it,
    /// matching the original form.zju.edu.cn encryption scheme.
    /// </summary>
    private static string EncryptTicket(string ticket)
    {
        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var input = Encoding.UTF8.GetBytes(ticket);
        var encrypted = encryptor.TransformFinalBlock(input, 0, input.Length);

        // Double Base64: Base64(Base64(encrypted_bytes))
        var firstBase64 = Convert.ToBase64String(encrypted);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(firstBase64));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        _loginLock.Dispose();
    }
}
