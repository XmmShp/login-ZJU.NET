using Microsoft.Extensions.Logging;

namespace LoginZju;

/// <summary>
/// Base class for ZJU services that authenticate via ZJUAM and use cookie-based sessions.
/// </summary>
public abstract class ZjuServiceBase : IZjuService
{
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _loggedIn;

    private protected ZjuServiceBase(IZjuamAuth auth, ILogger logger)
    {
        Auth = auth;
        Http = new CookieHttpClient();
        Logger = logger;
    }

    private protected IZjuamAuth Auth { get; }

    private protected CookieHttpClient Http { get; }

    private protected ILogger Logger { get; }

    /// <inheritdoc />
    public abstract Task LoginAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task<HttpResponseMessage> FetchAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureLoggedInAsync(cancellationToken).ConfigureAwait(false);
        return await Http.SendFollowingRedirectsAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private protected async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (_loggedIn)
        {
            return;
        }

        await _loginLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_loggedIn)
            {
                await LoginAsync(ct).ConfigureAwait(false);
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
            Http.Dispose();
            _loginLock.Dispose();
        }
    }
}
