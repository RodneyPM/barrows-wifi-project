using System.Net.Http.Headers;
using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using BarrowsWifi.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BarrowsWifi.Api.Tests;

/// <summary>
/// Hosts the real API (real routing, real JWT validation, real authorisation) over an in-memory database,
/// so tests exercise the same pipeline a client request goes through.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const int BarrowsOrg = 1;
    public const int KwelaOrg = 2;
    public const int ShisaOrg = 3;
    public const int KwelaCampaign = 1;
    public const int ShisaCampaign = 2;

    public static readonly User PlatformAdmin = new() { Id = 1, Email = "admin@test", Role = Roles.PlatformAdmin, OrganisationId = BarrowsOrg };
    public static readonly User KwelaAdmin = new() { Id = 2, Email = "ops@kwela.test", Role = Roles.ClientAdmin, OrganisationId = KwelaOrg };
    public static readonly User KwelaViewer = new() { Id = 3, Email = "viewer@kwela.test", Role = Roles.ClientViewer, OrganisationId = KwelaOrg };
    public static readonly User ShisaAdmin = new() { Id = 4, Email = "ops@shisa.test", Role = Roles.ClientAdmin, OrganisationId = ShisaOrg };

    private static readonly DateTimeOffset FlightStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(2));

    private readonly string _dbName = "tests-" + Guid.NewGuid();
    private readonly object _seedLock = new();
    private bool _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>A client signed in as the given user, or anonymous when user is null.</summary>
    public HttpClient ClientAs(User? user)
    {
        EnsureSeeded();
        var client = CreateClient();
        if (user is not null)
        {
            var (token, _) = Services.GetRequiredService<TokenService>().Issue(user);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    public void EnsureSeeded()
    {
        lock (_seedLock)
        {
            if (_seeded) return;
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Organisations.AddRange(
                new Organisation { Id = BarrowsOrg, Name = "Barrows WiFi", Type = "Internal" },
                new Organisation { Id = KwelaOrg, Name = "Kwela Cola" },
                new Organisation { Id = ShisaOrg, Name = "Shisa Lager" });
            db.Outlets.Add(new Outlet
            {
                Id = 1, StpNumber = "835903", Name = "Test Tavern", Province = "Gauteng",
                CreatedAt = FlightStart, UpdatedAt = FlightStart,
            });
            db.Campaigns.AddRange(
                NewCampaign(KwelaCampaign, KwelaOrg, "Summer Refresh"),
                NewCampaign(ShisaCampaign, ShisaOrg, "Lager Launch"));
            db.ConnectionEvents.AddRange(
                // Outlet 1 is in BOTH campaigns and both fly on the same dates (like Summer Refresh and
                // Heritage Month Braai in the real data), so "events at the outlet" mixes them up.
                NewEvent(KwelaCampaign, EventTypes.AdComplete, "dev-1", 4),
                NewEvent(KwelaCampaign, EventTypes.AdComplete, "dev-1", 5),
                NewEvent(KwelaCampaign, EventTypes.AdComplete, "dev-2", 6),
                NewEvent(KwelaCampaign, EventTypes.SurveyComplete, "dev-1", 6),
                NewEvent(ShisaCampaign, EventTypes.AdComplete, "dev-3", 7),
                NewEvent(ShisaCampaign, EventTypes.AdComplete, "dev-4", 8),
                // Exactly at the (exclusive) end of the flight: must not be counted.
                NewEvent(KwelaCampaign, EventTypes.AdComplete, "dev-5", 30));
            db.SaveChanges();
            _seeded = true;
        }
    }

    private static ConnectionEvent NewEvent(int campaignId, string type, string device, int daysIntoFlight)
    {
        var at = FlightStart.AddDays(daysIntoFlight);
        return new ConnectionEvent
        {
            EventId = Guid.NewGuid(), OutletId = 1, CampaignId = campaignId, DeviceHash = device,
            EventType = type, OccurredAt = at, ReceivedAt = at,
        };
    }

    // Draft, so the background lifecycle service leaves the seed data alone.
    private static Campaign NewCampaign(int id, int organisationId, string name) => new()
    {
        Id = id, OrganisationId = organisationId, Name = name, Status = CampaignStatus.Draft,
        StartsAt = FlightStart, EndsAt = FlightStart.AddDays(30), BudgetZar = 1000m,
        CreatedAt = FlightStart, UpdatedAt = FlightStart,
        Outlets = { new CampaignOutlet { OutletId = 1 } },
    };
}