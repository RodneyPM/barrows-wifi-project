using System.Net;
using System.Net.Http.Json;
using BarrowsWifi.Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarrowsWifi.Api.Tests;

/// <summary>
/// The portal retries on slow links, so the same event can reach the API more than once.
/// A repeat must be answered with success (so the portal stops retrying) but stored only once.
/// (The in-memory test database does not enforce the unique index from migration 006, so these tests cover
/// the endpoint's own check. The index itself is verified against SQL Server, see TICKET2 notes.)
/// </summary>
public class IngestIdempotencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IngestIdempotencyTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Same_event_sent_twice_is_stored_once_and_both_calls_succeed()
    {
        var client = PortalClient();
        var eventId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync("/api/portal/events", Payload(eventId));
        var second = await client.PostAsJsonAsync("/api/portal/events", Payload(eventId));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);   // success, so the portal stops retrying
        Assert.Equal(1, CountStored(eventId));
    }

    [Fact]
    public async Task Different_events_are_each_stored()
    {
        var client = PortalClient();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/portal/events", Payload(a));
        await client.PostAsJsonAsync("/api/portal/events", Payload(b));

        Assert.Equal(1, CountStored(a));
        Assert.Equal(1, CountStored(b));
    }

    private HttpClient PortalClient()
    {
        _factory.EnsureSeeded();
        var client = _factory.CreateClient();
        var key = _factory.Services.GetRequiredService<IConfiguration>()["Ingest:ApiKey"]!;
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static object Payload(Guid eventId) => new
    {
        eventId,
        stpNumber = "835903",
        campaignId = 1,
        deviceHash = "dev-ingest-test",
        eventType = "ad_complete",
        occurredAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.FromHours(2)),
        msisdn = (string?)null,
    };

    private int CountStored(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.ConnectionEvents.Count(e => e.EventId == eventId);
    }
}