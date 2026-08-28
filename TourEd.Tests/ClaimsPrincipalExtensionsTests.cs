using System.Security.Claims;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Extensions;

namespace TourEd.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void TryGetUserReturnsInternalUserClaims()
    {
        var principal = CreatePrincipal(
            new Claim(Constants.ClaimsNames.UserId, "42"),
            new Claim(Constants.ClaimsNames.UserEmail, "known@example.test"));

        var result = principal.TryGetUser(out var user);

        Assert.True(result);
        Assert.NotNull(user);
        Assert.Equal(42, user.Id);
        Assert.Equal("known@example.test", user.Email);
    }

    [Theory]
    [InlineData(null, "known@example.test")]
    [InlineData("invalid", "known@example.test")]
    [InlineData("0", "known@example.test")]
    [InlineData("42", null)]
    [InlineData("42", " ")]
    public void TryGetUserRejectsMissingOrInvalidClaims(string? userId, string? email)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(Constants.ClaimsNames.UserId, userId));
        }
        if (email is not null)
        {
            claims.Add(new Claim(Constants.ClaimsNames.UserEmail, email));
        }

        var result = CreatePrincipal(claims.ToArray()).TryGetUser(out var user);

        Assert.False(result);
        Assert.Null(user);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
