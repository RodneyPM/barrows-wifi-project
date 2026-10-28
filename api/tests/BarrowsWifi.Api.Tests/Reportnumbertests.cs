using System.Net.Http.Json;
using BarrowsWifi.Api.Infrastructure;
using Xunit;

namespace BarrowsWifi.Api.Tests;

/// <summary>
/// Report numbers must come from the campaign's own events only. In the test data both campaigns use
/// outlet 1 on the same dates, which is exactly how Summer Refresh picked up Heritage Month Braai's
/// ad completes in production-like data (4,703 reported vs 3,646 real).
/// </summary>
public class ReportNumbersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReportNumbersTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Report_ignores_other_campaigns_events_at_shared_outlets_and_the_exclusive_end()
    {
        var report = await _factory.ClientAs(ApiFactory.KwelaAdmin)
            .GetFromJsonAsync<CampaignReport>("/api/reports/campaigns/1/summary");

        Assert.NotNull(report);
        Assert.Equal(3, report!.Impressions);       // not 5: campaign 2's two ad completes are not ours
                                                    // and the one at the exclusive end is out
        Assert.Equal(2, report.UniqueUsers);        // dev-1 watched twice, dev-2 once
        Assert.Equal(1, report.SurveyCompletes);
    }

    [Fact]
    public async Task Other_campaign_on_the_same_outlet_gets_its_own_numbers()
    {
        var report = await _factory.ClientAs(ApiFactory.ShisaAdmin)
            .GetFromJsonAsync<CampaignReport>("/api/reports/campaigns/2/summary");

        Assert.NotNull(report);
        Assert.Equal(2, report!.Impressions);
        Assert.Equal(2, report.UniqueUsers);
        Assert.Equal(0, report.SurveyCompletes);
    }
}