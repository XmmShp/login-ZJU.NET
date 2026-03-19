using Microsoft.Extensions.Logging;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to 智云课堂 (classroom.zju.edu.cn).
/// </summary>
public interface IClassroomService : IZjuService;

/// <inheritdoc cref="IClassroomService" />
public sealed class ClassroomService : ZjuServiceBase, IClassroomService
{
    private const string EntryUrl =
        "https://tgmedia.cmc.zju.edu.cn/index.php?r=auth%2Flogin&forward=https%3A%2F%2Fclassroom.zju.edu.cn%2F";
    private const string ZjuamHost = "zjuam.zju.edu.cn";

    /// <summary>
    /// Initializes a new instance of <see cref="ClassroomService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Logger instance.</param>
    public ClassroomService(IZjuamAuth auth, ILogger<ClassroomService> logger)
        : base(auth, logger) { }

    /// <inheritdoc />
    public override async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[CLASSROOM] Login begins.");

        // Follow redirects from entry URL until we reach ZJUAM.
        var zjuamUrl = await Http.FollowRedirectsUntilHostAsync(EntryUrl, ZjuamHost, cancellationToken);
        Logger.LogDebug("[CLASSROOM] Redirected to ZJUAM: {Url}", zjuamUrl);

        // Authenticate via ZJUAM OAuth2.
        var callbackUrl = await Auth.LoginServiceOAuth2Async(zjuamUrl, cancellationToken);
        Logger.LogDebug("[CLASSROOM] Callback URL from ZJUAM: {Url}", callbackUrl);

        // Follow all remaining redirects, including meta-refresh.
        await Http.FollowAllRedirectsAsync(callbackUrl, cancellationToken);

        Logger.LogInformation("[CLASSROOM] Login finalized.");
    }
}
