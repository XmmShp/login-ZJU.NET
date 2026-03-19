namespace LoginZju;

/// <summary>
/// Provides ZJU unified identity authentication (zjuam.zju.edu.cn) and the ability
/// to authenticate with downstream ZJU services via CAS or OAuth2.
/// </summary>
public interface IZjuamAuth : IZjuService
{
    /// <summary>
    /// Authenticates with a CAS-based service and returns the callback URL containing the ticket.
    /// </summary>
    /// <param name="serviceUrl">The service URL to authenticate with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The callback URL with the service ticket.</returns>
    Task<string> LoginServiceAsync(string serviceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates with an OAuth2-based service by following ZJUAM redirects
    /// until the flow leaves the zjuam.zju.edu.cn domain.
    /// </summary>
    /// <param name="redirectUrl">The initial OAuth2 authorization URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final redirect URL outside the ZJUAM domain.</returns>
    Task<string> LoginServiceOAuth2Async(string redirectUrl, CancellationToken cancellationToken = default);
}
