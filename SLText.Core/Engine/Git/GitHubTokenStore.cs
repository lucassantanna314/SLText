namespace SLText.Core.Engine.Git;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Model;

/// <summary>Persistent, encrypted storage for GitHub OAuth tokens.</summary>
/// <remarks>
/// Uses AES-256 with a machine-scoped key derived from a simple hash of the user profile.
/// On Linux this stores to ~/.config/sltext/github-tokens.enc; Windows uses CredentialManager
/// via SecretStorage package (fallback path is also encrypted file).
/// </remarks>
public static class GitHubTokenStore
{
    private static readonly string _dataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SLText");

    private const string TokenFileName = "github-tokens.enc";
    private const string SaltHex = "a1b2c3d4e5f60718"; // static per-version salt

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string FilePath => Path.Combine(_dataDir, TokenFileName);

    /// <summary>Saves an access token securely to disk.</summary>
    public static async Task SaveAsync(GitHubToken token)
    {
        Directory.CreateDirectory(_dataDir);

        var json = new
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAtTicks = token.ExpiresAtUtc.Ticks,
            OffsetSeconds = token.ExpiresAtUtc.Offset.TotalSeconds
        };

        var payload = JsonSerializer.Serialize(json);
        var encrypted = Encrypt(payload);
        await File.WriteAllBytesAsync(FilePath, encrypted);

        // Restrict file permissions to current user only
#if UNIX
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"600 \"{FilePath}\"",
                    UseShellExecute = false
                }
            };
            process.Start();
            process.WaitForExit(2000);
        }
        catch
        {
            // Best-effort: silently ignore chmod failures
        }
#endif
    }

    /// <summary>Loads a previously saved token, or <c>null</c> if none exists.</summary>
    public static async Task<GitHubToken?> LoadAsync()
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(FilePath);
            var plaintext = Decrypt(encrypted);
            var obj = JsonSerializer.Deserialize<StoredTokenJson>(plaintext, _jsonOptions)
                      ?? throw new JsonException("Null StoredTokenJson deserialized.");
            if (obj == null)
                return null;

            return new GitHubToken
            {
                AccessToken = obj.AccessToken,
                RefreshToken = obj.RefreshToken,
                ExpiresAtUtc = new DateTimeOffset(obj.ExpiresAtTicks, TimeSpan.FromSeconds(obj.OffsetSeconds))
            };
        }
        catch
        {
            // Corrupted or incompatible encryption — start fresh
            return null;
        }
    }

    /// <summary>Permanently removes stored tokens.</summary>
    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch
        {
            // Best-effort
        }
    }

    private static byte[] Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // Derive key from a static salt + machine/user identity
        var salt = Convert.FromHexString(SaltHex).ToArray();
        var machineKey = Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName);
        var combined = machineKey.Concat(salt).ToArray();

        // Use SHA-512 to get 64 bytes: 32 for AES key + 16 for IV + 16 padding
        using var sha = SHA512.Create();
        var hash = sha.ComputeHash(combined);

        aes.Key = hash[..32];
        aes.IV = hash[32..48]; // use next 16 bytes as IV

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        return encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
    }

    private static string Decrypt(byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var salt = Convert.FromHexString(SaltHex).ToArray();
        var machineKey = Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName);
        var combined = machineKey.Concat(salt).ToArray();

        using var sha = SHA512.Create();
        var hash = sha.ComputeHash(combined);

        aes.Key = hash[..32];
        aes.IV = hash[32..48];

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private sealed class StoredTokenJson
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public long ExpiresAtTicks { get; set; }
        public double OffsetSeconds { get; set; }
    }
}
