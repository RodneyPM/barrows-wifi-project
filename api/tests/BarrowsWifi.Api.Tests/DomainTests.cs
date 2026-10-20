using BarrowsWifi.Api.Domain;
using BarrowsWifi.Api.Infrastructure;
using Xunit;

namespace BarrowsWifi.Api.Tests;

public class StpNumberTests
{
    [Theory]
    [InlineData("12345", "012345")]
    [InlineData(" 482913 ", "482913")]
    [InlineData("000042", "000042")]
    public void Normalises_valid_numbers(string input, string expected)
    {
        Assert.True(StpNumber.TryNormalise(input, out var stp));
        Assert.Equal(expected, stp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("ABC123")]
    public void Rejects_invalid_numbers(string input)
    {
        Assert.False(StpNumber.TryNormalise(input, out _));
    }
}

public class CampaignRulesTests
{
    [Fact]
    public void Ended_campaigns_cannot_be_reopened() =>
        Assert.False(CampaignRules.CanTransition(CampaignStatus.Ended, CampaignStatus.Live));

    [Fact]
    public void Draft_can_be_scheduled() =>
        Assert.True(CampaignRules.CanTransition(CampaignStatus.Draft, CampaignStatus.Scheduled));
}

public class PasswordHashingTests
{
    [Fact]
    public void Round_trips()
    {
        var hash = PasswordHashing.Hash("correct horse");
        Assert.True(PasswordHashing.Verify("correct horse", hash));
        Assert.False(PasswordHashing.Verify("wrong", hash));
    }
}
