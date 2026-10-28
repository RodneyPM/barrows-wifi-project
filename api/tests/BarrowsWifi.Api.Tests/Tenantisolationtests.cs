using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace BarrowsWifi.Api.Tests;

/// <summary>
/// One client must never be able to read or change another client's campaigns.
/// The first test below is the regression for the report leak (Kwela Cola's numbers shown to Shisa Lager).
/// </summary>
public class TenantIsolationTests : IClassFixture<ApiFactory>
{
    private const string KwelaReport = "/api/reports/campaigns/1/summary";
    private readonly ApiFactory _factory;

    public TenantIsolationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Client_cannot_read_another_clients_campaign_report()
    {
        var response = await _factory.ClientAs(ApiFactory.ShisaAdmin).GetAsync(KwelaReport);

        // 404, not 403: do not confirm to a competitor that the campaign exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Kwela", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Client_can_read_own_campaign_report()
    {
        var response = await _factory.ClientAs(ApiFactory.KwelaAdmin).GetAsync(KwelaReport);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Summer Refresh", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Platform_admin_can_read_any_clients_report()
    {
        var response = await _factory.ClientAs(ApiFactory.PlatformAdmin).GetAsync(KwelaReport);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Report_requires_sign_in()
    {
        var response = await _factory.ClientAs(null).GetAsync(KwelaReport);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_read_another_clients_campaign()
    {
        var response = await _factory.ClientAs(ApiFactory.ShisaAdmin).GetAsync("/api/campaigns/1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_edit_another_clients_campaign()
    {
        var response = await _factory.ClientAs(ApiFactory.ShisaAdmin).PutAsJsonAsync("/api/campaigns/1", ValidEdit());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_edit_campaigns()
    {
        var response = await _factory.ClientAs(ApiFactory.KwelaViewer).PutAsJsonAsync("/api/campaigns/1", ValidEdit());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static object ValidEdit() => new
    {
        name = "Renamed",
        startsAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(2)),
        endsAt = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.FromHours(2)),
        budgetZar = 500m,
        outletIds = new[] { 1 },
    };
}