using Microsoft.Extensions.Logging;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to 校园卡二维码页面 (yqfkgl.zju.edu.cn).
/// </summary>
public interface IYqfkglService : IZjuService;

/// <inheritdoc cref="IYqfkglService" />
public sealed class YqfkglService : ZjuServiceBase, IYqfkglService
{
    private const string ServiceUrl = "https://yqfkgl.zju.edu.cn/_web/_customizes/ykt/index3.jsp";

    /// <summary>
    /// Initializes a new instance of <see cref="YqfkglService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Logger instance.</param>
    public YqfkglService(IZjuamAuth auth, ILogger<YqfkglService> logger)
        : base(auth, logger) { }

    /// <inheritdoc />
    public override async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[YQFKGL] Login begins.");

        // Pre-visit the service URL to establish initial cookies.
        await Http.GetAsync(ServiceUrl, cancellationToken).ConfigureAwait(false);

        // Authenticate via ZJUAM CAS.
        var callbackUrl = await Auth.LoginServiceAsync(ServiceUrl, cancellationToken).ConfigureAwait(false);
        Logger.LogDebug("[YQFKGL] Callback URL: {Url}", callbackUrl);

        // Follow the callback to finalize the session.
        await Http.GetAsync(callbackUrl, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation("[YQFKGL] Login finalized.");
    }
}
