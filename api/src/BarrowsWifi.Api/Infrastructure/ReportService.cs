using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BarrowsWifi.Api.Infrastructure;

public record CampaignReport(
    int CampaignId,
    string CampaignName,
    string Organisation,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Outlets,
    int Impressions,
    int UniqueUsers,
    int SurveyCompletes);

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

   private async Task<CampaignReport> BuildAsync(Campaign campaign, CancellationToken ct)
    {
        var campaignId = campaign.Id;

        // Events at the campaign's outlets during the flight
        var events =
            from e in _db.ConnectionEvents
            join co in _db.CampaignOutlets on e.OutletId equals co.OutletId
            where co.CampaignId == campaignId
                  && e.OccurredAt >= campaign.StartsAt
                  && e.OccurredAt < campaign.EndsAt
            select e;

        var impressions = await events.CountAsync(e => e.EventType == EventTypes.AdComplete, ct);
        var uniqueUsers = await events
            .Where(e => e.EventType == EventTypes.AdComplete)
            .Select(e => e.DeviceHash)
            .Distinct()
            .CountAsync(ct);
        var surveys = await events.CountAsync(e => e.EventType == EventTypes.SurveyComplete, ct);
        var outlets = await _db.CampaignOutlets.CountAsync(co => co.CampaignId == campaignId, ct);

        return new CampaignReport(
            campaign.Id,
            campaign.Name,
            campaign.Organisation?.Name ?? "",
            campaign.StartsAt,
            campaign.EndsAt,
            outlets,
            impressions,
            uniqueUsers,
            surveys);
    }
    /// <summary>
    /// Report for a signed-in caller. Returns null when the campaign does not exist OR belongs to another
    /// organisation, so the endpoint answers 404 either way and does not reveal which ids exist.
    /// </summary>
    public async Task<CampaignReport?> BuildForUserAsync(int campaignId, CurrentUser user, CancellationToken ct)
    {
        var campaign = await _db.Campaigns
            .VisibleTo(user)
            .Include(c => c.Organisation)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        return campaign is null ? null : await BuildAsync(campaign, ct);
    }

    /// <summary>
    /// Report with NO organisation check. Only for trusted, system-initiated callers such as the
    /// CampaignEnded consumer, which has no signed-in user. Never call this from a request handler.
    /// </summary>
    public async Task<CampaignReport?> BuildForSystemAsync(int campaignId, CancellationToken ct)
    {
        var campaign = await _db.Campaigns
            .Include(c => c.Organisation)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        return campaign is null ? null : await BuildAsync(campaign, ct);
    }
}
