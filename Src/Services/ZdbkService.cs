using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to 本科教学管理信息服务平台 (zdbk.zju.edu.cn).
/// </summary>
public interface IZdbkService : IZjuService;

/// <inheritdoc cref="IZdbkService" />
public sealed class ZdbkService : ZjuServiceBase, IZdbkService
{
    private const string InitUrl = "https://zdbk.zju.edu.cn/jwglxt/xtgl/login_cxSsoLoginUrl.html";
    private const string ServiceUrl = "https://zdbk.zju.edu.cn/jwglxt/xtgl/login_ssologin.html";
    private const string ExpectedRedirectPrefix = "https://zdbk.zju.edu.cn/jwglxt/xtgl/index_initMenu.html";

    /// <summary>
    /// Initializes a new instance of <see cref="ZdbkService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Optional logger.</param>
    public ZdbkService(IZjuamAuth auth, ILogger<ZdbkService>? logger = null)
        : base(auth, logger ?? NullLogger<ZdbkService>.Instance) { }

    /// <inheritdoc />
    public override async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[ZDBK] Login begins.");

        // Initialize session.
        await Http.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, InitUrl), cancellationToken);

        // Authenticate via ZJUAM CAS.
        var callbackUrl = await Auth.LoginServiceAsync(ServiceUrl, cancellationToken);
        Logger.LogDebug("[ZDBK] Callback URL: {Url}", callbackUrl);

        var response = await Http.GetAsync(callbackUrl, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Found)
        {
            throw new LoginException($"[ZDBK] Login failed, status code {(int)response.StatusCode}.");
        }

        var location = response.Headers.Location?.ToString();
        if (location is null || !location.Contains(ExpectedRedirectPrefix))
        {
            throw new LoginException($"[ZDBK] Login failed, unexpected redirect to {location}.");
        }

        Logger.LogInformation("[ZDBK] Login successful.");
    }
}
