using BarrowsWifi.Api.Infrastructure;

namespace BarrowsWifi.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization();

        // Campaign performance summary shown on the client reporting screen.
        group.MapGet("/campaigns/{id:int}/summary", async (int id, ReportService reports, CancellationToken ct) =>
        {
            var report = await reports.BuildAsync(id, ct);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });
    }
}
