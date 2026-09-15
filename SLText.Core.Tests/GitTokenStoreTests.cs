using SLText.Core.Engine.Git;
using SLText.Core.Engine.Model;
using Xunit;

namespace SLText.Core.Tests;

/// <summary>
/// Tests for GitHubTokenStore encryption/decryption round-trip.
/// Verifies tokens survive save → load without modification.
/// </summary>
public class GitTokenStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesTokenValues()
    {
        // Arrange — a realistic-looking token
        var original = new GitHubToken
        {
            AccessToken = "ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef123456",
            RefreshToken = "ghr_RefreshTokenValueHere123",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        try
        {
            // Act
            await GitHubTokenStore.SaveAsync(original);
            var loaded = await GitHubTokenStore.LoadAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(original.AccessToken, loaded.AccessToken);
            Assert.Equal(original.RefreshToken, loaded.RefreshToken);

            // Verify expiry hasn't passed
            var now = DateTimeOffset.UtcNow;
            var diff = loaded.ExpiresAtUtc - now;
            Assert.True(diff > TimeSpan.Zero, $"Token expired: now={now}, expiresAt={loaded.ExpiresAtUtc}, diff={diff.TotalMilliseconds}ms");
        }
        finally
        {
            // Cleanup to avoid stale state affecting other tests
            GitHubTokenStore.Clear();
        }
    }

    [Fact]
    public async Task LoadWhenNoFile_ReturnsNull()
    {
        try
        {
            GitHubTokenStore.Clear(); // Ensure clean slate

            var result = await GitHubTokenStore.LoadAsync();

            Assert.Null(result);
        }
        finally
        {
            GitHubTokenStore.Clear();
        }
    }

    [Fact]
    public async Task Clear_RemovesStoredFile()
    {
        try
        {
            var token = new GitHubToken
            {
                AccessToken = "ghp_TestToken",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
            };

            await GitHubTokenStore.SaveAsync(token);
            GitHubTokenStore.Clear();

            var loaded = await GitHubTokenStore.LoadAsync();
            Assert.Null(loaded);
        }
        finally
        {
            GitHubTokenStore.Clear();
        }
    }

    [Fact]
    public void IsExpired_DetectsPastExpiry()
    {
        var expired = new GitHubToken
        {
            AccessToken = "ghp_Test",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        Assert.True(expired.IsExpired);
    }

    [Fact]
    public void IsNotExpired_WithFutureExpiry()
    {
        var valid = new GitHubToken
        {
            AccessToken = "ghp_Test",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        Assert.False(valid.IsExpired);
    }
}
