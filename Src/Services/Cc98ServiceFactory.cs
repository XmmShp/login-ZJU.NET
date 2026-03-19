using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoginZju;

/// <summary>
/// Factory for creating per-user <see cref="ICc98Service"/> instances.
/// The app-level OAuth2 client credentials (ClientId, ClientSecret) are injected via options;
/// per-user credentials (username, password) are provided at creation time.
/// </summary>
public interface ICc98ServiceFactory
{
    /// <summary>
    /// Creates a new <see cref="ICc98Service"/> instance for the specified CC98 user.
    /// </summary>
    /// <param name="username">CC98 username.</param>
    /// <param name="password">CC98 password.</param>
    ICc98Service Create(string username, string password);
}

/// <inheritdoc cref="ICc98ServiceFactory" />
public sealed class Cc98ServiceFactory : ICc98ServiceFactory
{
    private readonly Cc98Options _options;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="Cc98ServiceFactory"/>.
    /// </summary>
    /// <param name="options">App-level CC98 options (ClientId, ClientSecret, TokenUrl).</param>
    /// <param name="loggerFactory">Logger factory for creating typed loggers.</param>
    public Cc98ServiceFactory(IOptions<Cc98Options> options, ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public ICc98Service Create(string username, string password)
        => new Cc98Service(username, password, _options, _loggerFactory.CreateLogger<Cc98Service>());
}
