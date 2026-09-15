namespace SLText.Core.Engine.Git;

using System.Net.Http.Headers;
using System.Text.Json;

/// <summary>
/// OAuth 2.0 Device Flow authentication with GitHub.
/// Uses POST /login/oauth/device/code → poll /login/oauth/access_token pattern.
/// </summary>
public class GitHubAuthService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private const string ClientId = "Ov23lidalHtPxk5xV7S68mK2"; // from user's registered app — change as needed

    private static readonly string TokenUrl = "https://github.com/login/oauth/access_token";
    private static readonly string DeviceCodeUrl = "https://github.com/login/oauth/device/code";

    public GitHubToken? CurrentToken { get; private set; }

    /// <summary>Fired when authentication state changes (logged in or out).</summary>
    public event EventHandler<bool>? AuthStateChanged;

    /// <summary>Callback to display device code to user for manual entry.</summary>
    public delegate void DeviceFlowCallback(string userCode, Uri verificationUri);

    public GitHubAuthService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new();
    }

    /// <summary>Initiate OAuth 2.0 Device Flow and poll for token until success or expiration.</summary>
    public async Task AuthenticateViaDeviceFlowAsync(DeviceFlowCallback callback)
    {
        await _gate.WaitAsync();
        try
        {
            // Step 1: Request device code
            var deviceRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("scope", "repo read:org")
            });

            var deviceResponse = await _httpClient.PostAsync(DeviceCodeUrl, deviceRequest);
            deviceResponse.EnsureSuccessStatusCode();
            var deviceBody = await deviceResponse.Content.ReadAsStringAsync();
            var deviceParams = ParseFormUrlEncoded(deviceBody);

            var deviceCode = deviceParams["device_code"]!;
            var userCode = deviceParams["user_code"]!;
            var verificationUri = new Uri(deviceParams["verification_uri"]!);

            // Notify view layer to show code to user
            callback?.Invoke(userCode, verificationUri);
            AuthStateChanged?.Invoke(this, false);

            // Step 2: Poll for token
            var interval = int.TryParse(deviceParams["interval"], out var i) ? i : 5;

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval));
                await _gate.WaitAsync();

                try
                {
                    var pollRequest = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("client_id", ClientId),
                        new KeyValuePair<string, string>("device_code", deviceCode),
                        new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                    });

                    var pollResponse = await _httpClient.PostAsync(TokenUrl, pollRequest);
                    var pollBody = await pollResponse.Content.ReadAsStringAsync();
                    var pollParams = ParseFormUrlEncoded(pollBody);

                    var grantType = pollParams.GetValueOrDefault("grant_type");
                    if (grantType == "authorization_pending")
                        continue;

                    var error = pollParams.GetValueOrDefault("error");
                    if (pollResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized || !string.IsNullOrEmpty(error))
                    {
                        if (error == "authorization_pending")
                            continue;
                        if (error == "expired_device_code" || error == "approval_expired")
                            throw new InvalidOperationException("Auth code expired. Please retry.");
                        if (error == "access_denied")
                            throw new InvalidOperationException("User denied authorization.");
                        throw new HttpRequestException($"Device flow error: {error}");
                    }

                    pollResponse.EnsureSuccessStatusCode();

                    // Success: store tokens
                    CurrentToken = new GitHubToken
                    {
                        AccessToken = pollParams["access_token"]!,
                        RefreshToken = pollParams.GetValueOrDefault("refresh_token"),
                        ExpiresAtUtc = DateTime.UtcNow.AddSeconds(int.Parse(pollParams["expires_in"]))
                    };

                    AuthStateChanged?.Invoke(this, true);
                    return;
                }
                finally
                {
                    _gate.Release();
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Refresh an expired access token using the stored refresh token.</summary>
    public async Task RefreshAccessTokenAsync()
    {
        if (CurrentToken == null || string.IsNullOrEmpty(CurrentToken.RefreshToken))
            throw new InvalidOperationException("No refresh token available.");

        await _gate.WaitAsync();
        try
        {
            var request = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("refresh_token", CurrentToken.RefreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            var response = await _httpClient.PostAsync(TokenUrl, request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var paramsMap = ParseFormUrlEncoded(body);

            CurrentToken.AccessToken = paramsMap["access_token"]!;
            if (paramsMap.TryGetValue("refresh_token", out var newRefresh))
                CurrentToken.RefreshToken = newRefresh;
            CurrentToken.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(int.Parse(paramsMap["expires_in"]));

            // Persist the refreshed token
            await GitHubTokenStore.SaveAsync(CurrentToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Clear stored credentials and reset auth state.</summary>
    public void ClearTokens()
    {
        CurrentToken = null;
        AuthStateChanged?.Invoke(this, false);
        GitHubTokenStore.Clear();
    }

    /// <summary>True when a valid (non-expired) token is available.</summary>
    public bool IsAuthenticated => CurrentToken != null && !CurrentToken.IsExpired;

    public void Dispose()
    {
        _gate.Dispose();
        _httpClient.Dispose();
    }

    /// <summary>Parse form-urlencoded content into a dictionary.</summary>
    private static Dictionary<string, string?> ParseFormUrlEncoded(string body)
    {
        var result = new Dictionary<string, string?>();
        if (string.IsNullOrEmpty(body))
            return result;

        foreach (var pair in body.Split('&'))
        {
            var eqIdx = pair.IndexOf('=');
            if (eqIdx < 0)
            {
                result[pair] = null;
            }
            else
            {
                var key = Uri.UnescapeDataString(pair[..eqIdx]);
                var value = Uri.UnescapeDataString(pair[(eqIdx + 1)..]);
                result[key] = value;
            }
        }
        return result;
    }
}
