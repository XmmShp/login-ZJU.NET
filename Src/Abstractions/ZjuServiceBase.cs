using Microsoft.Extensions.Logging;

namespace LoginZju;

/// <summary>
/// Base class for ZJU services that authenticate via ZJUAM and use cookie-based sessions.
/// </summary>
public abstract class ZjuServiceBase : IZjuService
{
    private readonly CookieHttpClient _http;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _loggedIn;

    private protected ZjuServiceBase(IZjuamAuth auth, ILogger logger)
    {
        Auth = auth;
        _http = new CookieHttpClient();
        Logger = logger;
    }

    private protected IZjuamAuth Auth { get; }

    private protected CookieHttpClient Http => _http;

    private protected ILogger Logger { get; }

    /// <inheritdoc />
    public abstract Task LoginAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task<HttpResponseMessage> FetchAsync(
        string url,
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoggedInAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        configureRequest?.Invoke(request);
        return await _http.SendFollowingRedirectsAsync(request, cancellationToken);
    }

    private protected async Task EnsureLoggedInAsync(CancellationToken ct)
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
                await LoginAsync(ct);
                _loggedIn = true;
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _http.Dispose();
            _loginLock.Dispose();
        }
    }
}
