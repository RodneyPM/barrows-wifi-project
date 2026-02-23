using System.Security.Claims;
using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using BarrowsWifi.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Endpoints;

public record CampaignListItem(int Id, string Name, string Organisation, string Status,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, decimal BudgetZar, int OutletCount);

public record CampaignDetail(int Id, int OrganisationId, string Name, string Status,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, decimal BudgetZar, int[] OutletIds,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record SaveCampaignRequest(
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    decimal BudgetZar,
    int[] OutletIds,
    string? Status,
    int? OrganisationId);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaigns").WithTags("Campaigns").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, string? status, int? page, int? pageSize, CancellationToken ct) =>
        {
            var user = CurrentUser.From(principal);
            var p = page is null or < 1 ? 1 : page.Value;
            var size = pageSize is null or < 1 or > 100 ? 25 : pageSize.Value;

            var query = db.Campaigns.Include(c => c.Organisation).VisibleTo(user);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);

            var total = await query.CountAsync(ct);
            var campaigns = await query
                .OrderByDescending(c => c.StartsAt)
                .Skip((p - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            var items = new List<CampaignListItem>();
            foreach (var c in campaigns)
            {
                var outletCount = await db.CampaignOutlets.CountAsync(co => co.CampaignId == c.Id, ct);
                items.Add(new CampaignListItem(c.Id, c.Name, c.Organisation!.Name, c.Status,
                    c.StartsAt, c.EndsAt, c.BudgetZar, outletCount));
            }

            return Results.Ok(new PagedResult<CampaignListItem>(items, p, size, total));
        });

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = CurrentUser.From(principal);
            var c = await db.Campaigns.VisibleTo(user)
                .Include(x => x.Outlets)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return c is null ? Results.NotFound() : Results.Ok(ToDetail(c));
        });

        group.MapPost("/", async (SaveCampaignRequest req, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = CurrentUser.From(principal);
            var errors = await ValidateAsync(req, db, ct);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var now = DateTimeOffset.UtcNow;
            var campaign = new Campaign
            {
                OrganisationId = user.IsPlatformAdmin && req.OrganisationId.HasValue ? req.OrganisationId.Value : user.OrganisationId,
                Name = req.Name.Trim(),
                Status = CampaignStatus.Draft,
                StartsAt = req.StartsAt,
                EndsAt = req.EndsAt,
                BudgetZar = req.BudgetZar,
                CreatedAt = now,
                UpdatedAt = now,
                Outlets = req.OutletIds.Distinct().Select(id => new CampaignOutlet { OutletId = id }).ToList(),
            };
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/campaigns/{campaign.Id}", ToDetail(campaign));
        }).RequireAuthorization("CanEditCampaigns");

        group.MapPut("/{id:int}", async (int id, SaveCampaignRequest req, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = CurrentUser.From(principal);
            var campaign = await db.Campaigns.VisibleTo(user)
                .Include(x => x.Outlets)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (campaign is null) return Results.NotFound();

            var errors = await ValidateAsync(req, db, ct);
            if (!string.IsNullOrWhiteSpace(req.Status) && req.Status != campaign.Status
                && !CampaignRules.CanTransition(campaign.Status, req.Status))
            {
                errors["status"] = new[] { $"Cannot move a campaign from {campaign.Status} to {req.Status}." };
            }
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            campaign.Name = req.Name.Trim();
            campaign.StartsAt = req.StartsAt;
            campaign.EndsAt = req.EndsAt;
            campaign.BudgetZar = req.BudgetZar;
            if (!string.IsNullOrWhiteSpace(req.Status)) campaign.Status = req.Status;
            campaign.UpdatedAt = DateTimeOffset.UtcNow;

            var wanted = req.OutletIds.Distinct().ToHashSet();
            campaign.Outlets.RemoveAll(o => !wanted.Contains(o.OutletId));
            foreach (var outletId in wanted.Where(w => campaign.Outlets.All(o => o.OutletId != w)))
                campaign.Outlets.Add(new CampaignOutlet { CampaignId = campaign.Id, OutletId = outletId });

            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDetail(campaign));
        });
    }

    private static CampaignDetail ToDetail(Campaign c) => new(
        c.Id, c.OrganisationId, c.Name, c.Status, c.StartsAt, c.EndsAt, c.BudgetZar,
        c.Outlets.Select(o => o.OutletId).ToArray(), c.CreatedAt, c.UpdatedAt);

    private static async Task<Dictionary<string, string[]>> ValidateAsync(SaveCampaignRequest req, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Name)) errors["name"] = new[] { "Name is required." };
        if (req.EndsAt <= req.StartsAt) errors["endsAt"] = new[] { "End must be after start." };
        if (req.BudgetZar <= 0) errors["budgetZar"] = new[] { "Budget must be greater than zero." };
        if (req.OutletIds is null || req.OutletIds.Length == 0)
        {
            errors["outletIds"] = new[] { "Select at least one outlet." };
        }
        else
        {
            var ids = req.OutletIds.Distinct().ToArray();
            var found = await db.Outlets.CountAsync(o => ids.Contains(o.Id) && o.IsActive, ct);
            if (found != ids.Length) errors["outletIds"] = new[] { "One or more outlets are unknown or inactive." };
        }
        return errors;
    }
}
