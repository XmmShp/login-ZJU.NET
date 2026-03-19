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
    /// Sends an authenticated HTTP request.
    /// Automatically calls <see cref="LoginAsync"/> on first use if not already logged in.
    /// </summary>
    /// <param name="request">The request message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HttpResponseMessage> FetchAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
