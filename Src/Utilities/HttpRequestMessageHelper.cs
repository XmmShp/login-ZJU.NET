namespace LoginZju;

/// <summary>
/// Provides utility helpers for <see cref="HttpRequestMessage"/>.
/// </summary>
internal static class HttpRequestMessageHelper
{
    /// <summary>
    /// Creates a deep clone of an HTTP request message, including headers and buffered content.
    /// </summary>
    public static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;

            clone.Content = new StreamContent(ms);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
