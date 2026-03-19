namespace LoginZju;

/// <summary>
/// Represents an error that occurred during authentication with a ZJU service.
/// </summary>
public class LoginException : Exception
{
    /// <inheritdoc />
    public LoginException(string message) : base(message) { }

    /// <inheritdoc />
    public LoginException(string message, Exception innerException) : base(message, innerException) { }
}
