using BarrowsWifi.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Endpoints;

public record OutletDto(int Id, string StpNumber, string Name, string Province, string ApStatus, bool IsActive,
    decimal? Latitude, decimal? Longitude);


public static class OutletEndpoints
{
    public static void MapOutletEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/outlets")
            .WithTags("Outlets")
            .RequireAuthorization();

        // Search outlets and return one page at a time.
        group.MapGet("/", async (
            string? search,
            int? page,
            int? pageSize,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentPage = page is null or < 1 ? 1 : page.Value;
            var size = pageSize is null or < 1 or > 100
                ? 25
                : pageSize.Value;

            var query = db.Outlets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                query = query.Where(o =>
                    o.Name.Contains(term) ||
                    o.StpNumber.Contains(term) ||
                    o.Province.Contains(term));
            }

            var total = await query.CountAsync(ct);

            var outlets = await query
                .OrderBy(o => o.Name)
                .ThenBy(o => o.Id)
                .Skip((currentPage - 1) * size)
                .Take(size)
                .Select(o => new OutletDto(
                    o.Id,
                    o.StpNumber,
                    o.Name,
                    o.Province,
                    o.ApStatus,
                    o.IsActive,
                    o.Latitude,
                    o.Longitude))
                .ToListAsync(ct);

            return Results.Ok(
                new PagedResult<OutletDto>(
                    outlets,
                    currentPage,
                    size,
                    total));
        });

        group.MapGet("/{id:int}", async (
            int id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var outlet = await db.Outlets
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(o => new OutletDto(
                    o.Id,
                    o.StpNumber,
                    o.Name,
                    o.Province,
                    o.ApStatus,
                    o.IsActive,
                    o.Latitude,
                    o.Longitude))
                .FirstOrDefaultAsync(ct);

            return outlet is null
                ? Results.NotFound()
                : Results.Ok(outlet);
        });
    }
}

