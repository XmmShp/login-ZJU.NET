using Microsoft.Extensions.Logging;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to 学在浙大 (courses.zju.edu.cn).
/// </summary>
public interface ICoursesService : IZjuService;

/// <inheritdoc cref="ICoursesService" />
public sealed class CoursesService : ZjuServiceBase, ICoursesService
{
    private const string EntryUrl = "https://courses.zju.edu.cn/user/index";
    private const string ZjuamHost = "zjuam.zju.edu.cn";

    /// <summary>
    /// Initializes a new instance of <see cref="CoursesService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Logger instance.</param>
    public CoursesService(IZjuamAuth auth, ILogger<CoursesService> logger)
        : base(auth, logger) { }

    /// <inheritdoc />
    public override async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[COURSES] Login begins.");

        // Follow redirects from entry URL until we reach ZJUAM.
        var zjuamUrl = await Http.FollowRedirectsUntilHostAsync(EntryUrl, ZjuamHost, cancellationToken).ConfigureAwait(false);
        Logger.LogDebug("[COURSES] Redirected to ZJUAM: {Url}", zjuamUrl);

        // Extract the service parameter and authenticate via ZJUAM.
        var service = new Uri(zjuamUrl).Query
            .Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p[0].TrimStart('?') == "service")
            .Select(p => Uri.UnescapeDataString(p[1]))
            .FirstOrDefault()
            ?? throw new LoginException("[COURSES] Could not extract service parameter from ZJUAM URL.");

        var callbackUrl = await Auth.LoginServiceAsync(service, cancellationToken).ConfigureAwait(false);
        Logger.LogDebug("[COURSES] Callback URL from ZJUAM: {Url}", callbackUrl);

        // Follow all remaining redirects, including meta-refresh.
        await Http.FollowAllRedirectsAsync(callbackUrl, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation("[COURSES] Login finalized.");
    }
}
