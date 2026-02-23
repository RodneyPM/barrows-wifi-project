using BarrowsWifi.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Endpoints;

public record OutletDto(int Id, string StpNumber, string Name, string Province, string ApStatus, bool IsActive,
    decimal? Latitude, decimal? Longitude);

public static class OutletEndpoints
{
    public static void MapOutletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/outlets").WithTags("Outlets").RequireAuthorization();

        // Used by the campaign wizard's outlet picker.
        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var outlets = await db.Outlets
                .OrderBy(o => o.Name)
                .Select(o => new OutletDto(o.Id, o.StpNumber, o.Name, o.Province, o.ApStatus, o.IsActive, o.Latitude, o.Longitude))
                .ToListAsync(ct);
            return Results.Ok(outlets);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var o = await db.Outlets.FindAsync(new object[] { id }, ct);
            return o is null
                ? Results.NotFound()
                : Results.Ok(new OutletDto(o.Id, o.StpNumber, o.Name, o.Province, o.ApStatus, o.IsActive, o.Latitude, o.Longitude));
        });
    }
}
