using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace LoginZju;

/// <summary>
/// Provides authenticated access to ETA 三全育人平台 (eta.zju.edu.cn).
/// </summary>
public interface IEtaService : IZjuService;

/// <inheritdoc cref="IEtaService" />
/// <remarks>
/// Also provides static <see cref="Encode"/> and <see cref="Decode"/> helpers for the platform's AES encryption.
/// </remarks>
public sealed class EtaService : ZjuServiceBase, IEtaService
{
    private const string CheckUrl = "http://eta.zju.edu.cn/zftal-xgxt-web/teacher/xtgl/index/check.zf";
    private static readonly byte[] AesKeyAndIv = "0123456789ABCDEF"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of <see cref="EtaService"/>.
    /// </summary>
    /// <param name="auth">An authenticated <see cref="IZjuamAuth"/> instance.</param>
    /// <param name="logger">Logger instance.</param>
    public EtaService(IZjuamAuth auth, ILogger<EtaService> logger)
        : base(auth, logger) { }

    /// <inheritdoc />
    public override async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[ETA] Login begins.");

        var callbackUrl = await Auth.LoginServiceAsync(CheckUrl, cancellationToken);
        Logger.LogDebug("[ETA] Callback URL: {Url}", callbackUrl);

        // Follow all redirects to finalize the session.
        await Http.FollowAllRedirectsAsync(callbackUrl, cancellationToken);

        Logger.LogInformation("[ETA] Login finalized.");
    }

    /// <summary>
    /// Encrypts a plaintext string using the ETA platform's AES-128-CBC scheme (zero-padded).
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <returns>Base64-encoded ciphertext.</returns>
    public static string Encode(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = AesKeyAndIv;
        aes.IV = AesKeyAndIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        var input = Encoding.UTF8.GetBytes(plaintext);
        var padLen = 16 - (input.Length % 16);
        var padded = new byte[input.Length + padLen];
        Array.Copy(input, padded, input.Length);
        // Remaining bytes are already zero (default).

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(padded, 0, padded.Length);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext using the ETA platform's AES-128-CBC scheme.
    /// </summary>
    /// <param name="ciphertext">The Base64-encoded ciphertext.</param>
    /// <returns>The decrypted plaintext.</returns>
    public static string Decode(string ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key = AesKeyAndIv;
        aes.IV = AesKeyAndIv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        var encrypted = Convert.FromBase64String(ciphertext);

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

        // Remove trailing zero-padding.
        var end = decrypted.Length;
        while (end > 0 && decrypted[end - 1] == 0)
        {
            end--;
        }

        return Encoding.UTF8.GetString(decrypted, 0, end);
    }
}
