using PaymentFlow.Domain.Entities;
using Xunit;

namespace PaymentFlow.Domain.Tests;

public class RefreshTokenTests
{
    private static RefreshToken CreateToken(DateTime expiresAtUtc) => new()
    {
        UserId = Guid.NewGuid(),
        TokenHash = "hash",
        ExpiresAtUtc = expiresAtUtc
    };

    [Fact]
    public void IsActive_ReturnsTrue_WhenNotExpiredAndNotRevoked()
    {
        var token = CreateToken(DateTime.UtcNow.AddDays(1));
        Assert.True(token.IsActive(DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenExpired()
    {
        var token = CreateToken(DateTime.UtcNow.AddMinutes(-1));
        Assert.False(token.IsActive(DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_ReturnsFalse_AfterRevocation()
    {
        var token = CreateToken(DateTime.UtcNow.AddDays(1));
        token.Revoke(DateTime.UtcNow, replacedByTokenHash: "newHash");

        Assert.False(token.IsActive(DateTime.UtcNow));
        Assert.Equal("newHash", token.ReplacedByTokenHash);
        Assert.NotNull(token.RevokedAtUtc);
    }
}
