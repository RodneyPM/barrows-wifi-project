using BarrowsWifi.Api.Infrastructure;
using System.Security.Claims;
namespace BarrowsWifi.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization();

        // Campaign performance summary shown on the client reporting screen.
        group.MapGet("/campaigns/{id:int}/summary", async (int id, ClaimsPrincipal principal, ReportService reports, CancellationToken ct) =>
        {
            var report = await reports.BuildForUserAsync(id, CurrentUser.From(principal), ct);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });
    }
}
