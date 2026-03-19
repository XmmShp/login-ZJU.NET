namespace LoginZju;

/// <summary>
/// Application-level configuration options for CC98 论坛 (cc98.org).
/// Contains OAuth2 client credentials (shared across all users).
/// Per-user credentials (username/password) are provided at runtime via the factory.
/// </summary>
public sealed class Cc98Options
{
    /// <summary>
    /// OAuth2 client ID for CC98 OpenID Connect.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 client secret for CC98 OpenID Connect.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
