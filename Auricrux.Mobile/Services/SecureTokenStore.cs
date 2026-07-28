using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace Auricrux.Mobile.Services;

/// <summary>
/// Mobile OIDC token storage path (AUX-021). Persists the access token issued after
/// interactive sign-in in the platform secure keystore (Android Keystore / iOS Keychain /
/// Windows DPAPI via <see cref="SecureStorage"/>) so the app can stay signed in across
/// launches and attach the bearer token to Auricrux API calls.
/// </summary>
public sealed class SecureTokenStore
{
    private const string AccessTokenKey = "auricrux_access_token";
    private readonly ILogger<SecureTokenStore> _logger;

    public SecureTokenStore(ILogger<SecureTokenStore> logger)
    {
        _logger = logger;
    }

    public async Task SaveTokenAsync(string accessToken)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist access token to secure storage");
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(AccessTokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No stored access token available");
            return null;
        }
    }

    public void ClearToken()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clear stored access token");
        }
    }
}
