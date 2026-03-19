namespace LoginZju;

/// <summary>
/// Represents a ZJU service that supports authentication and authenticated HTTP requests.
/// </summary>
public interface IZjuService : IDisposable
{
    /// <summary>
    /// Performs the login/authentication flow for this service.
    /// </summary>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an authenticated HTTP request to the specified URL.
    /// Automatically calls <see cref="LoginAsync"/> on first use if not already logged in.
    /// </summary>
    /// <param name="url">The target URL.</param>
    /// <param name="configureRequest">Optional action to configure the <see cref="HttpRequestMessage"/> (e.g. set method, headers, body).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HttpResponseMessage> FetchAsync(
        string url,
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken cancellationToken = default);
}
