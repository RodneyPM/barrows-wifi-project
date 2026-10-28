using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Endpoints;

/// <summary>Event as posted by the captive portal.</summary>
public record IngestEventRequest(
    Guid EventId,
    string StpNumber,
    int? CampaignId,
    string DeviceHash,
    string EventType,
    DateTimeOffset OccurredAt,
    string? Msisdn);

public record PortalConfig(string StpNumber, string OutletName, int? CampaignId, string? CampaignName, string? Sponsor);

public static class PortalEndpoints
{
    public static void MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").WithTags("Captive portal")
            .AddEndpointFilter<ApiKeyFilter>();

        // What the portal should show at an outlet right now.
        group.MapGet("/{stp}", async (string stp, AppDbContext db, CancellationToken ct) =>
        {
            if (!StpNumber.TryNormalise(stp, out var normalised)) return Results.BadRequest("Invalid STP number.");
            var outlet = await db.Outlets.FirstOrDefaultAsync(o => o.StpNumber == normalised, ct);
            if (outlet is null) return Results.NotFound();

            var now = DateTimeOffset.UtcNow;
            var campaign = await db.Campaigns
                .Include(c => c.Organisation)
                .Where(c => c.Outlets.Any(co => co.OutletId == outlet.Id))
                .Where(c => c.Status == CampaignStatus.Live && c.StartsAt <= now && c.EndsAt > now)
                .OrderBy(c => c.StartsAt)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new PortalConfig(outlet.StpNumber, outlet.Name, campaign?.Id, campaign?.Name, campaign?.Organisation?.Name));
        });

        group.MapPost("/events", async (IngestEventRequest req, AppDbContext db, ILogger<IngestEventRequest> log, CancellationToken ct) =>
        {
            log.LogInformation("Portal event received {@Event}", req);

            if (!EventTypes.All.Contains(req.EventType)) return Results.BadRequest("Unknown event type.");
            if (!StpNumber.TryNormalise(req.StpNumber, out var stp)) return Results.BadRequest("Invalid STP number.");

            var outletId = await db.Outlets.Where(o => o.StpNumber == stp).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
            if (outletId is null) return Results.BadRequest("Unknown outlet.");
// a repeat is acknowledged but not stored again           
if (await db.ConnectionEvents.AnyAsync(e => e.EventId == req.EventId, ct)) return Results.Accepted();
            db.ConnectionEvents.Add(new ConnectionEvent
            {
                EventId = req.EventId,
                OutletId = outletId.Value,
                CampaignId = req.CampaignId,
                DeviceHash = req.DeviceHash,
                EventType = req.EventType,
                OccurredAt = req.OccurredAt,
                Msisdn = req.Msisdn,
            });
            try { await db.SaveChangesAsync(ct); }
catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
{
    return Results.Accepted();   // two retries raced; the unique index let one in
}
return Results.Accepted();
        });
    }
}

/// <summary>Portal calls authenticate with a shared API key in the X-Api-Key header.</summary>
public class ApiKeyFilter : IEndpointFilter
{
    private readonly IConfiguration _config;

    public ApiKeyFilter(IConfiguration config) => _config = config;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers["X-Api-Key"].ToString();
        if (provided != _config["Ingest:ApiKey"]) return Results.Unauthorized();
        return await next(context);
    }
}
