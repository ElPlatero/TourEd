using Api.Authentication;

namespace TourEd.Tests;

public sealed class CliTokenComparerTests
{
    [Fact]
    public void MatchingTokenIsAccepted()
    {
        const string token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        Assert.True(CliTokenComparer.Matches(token, token));
    }

    [Theory]
    [InlineData("different-token")]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdee")]
    public void DifferentTokenIsRejected(string presentedToken)
    {
        const string configuredToken = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        Assert.False(CliTokenComparer.Matches(presentedToken, configuredToken));
    }
}
