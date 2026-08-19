using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Messaging;

/// <summary>Published when a campaign's flight has finished.</summary>
public record CampaignEnded(int CampaignId, DateTimeOffset EndedAt);

/// <summary>
/// Moves campaigns through their lifecycle on a timer:
/// Scheduled -> Live when the flight starts, Live -> Ended when it finishes.
/// </summary>
public class CampaignLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CampaignLifecycleService> _log;
    private readonly TimeSpan _interval;

    public CampaignLifecycleService(IServiceScopeFactory scopes, ILogger<CampaignLifecycleService> log, IConfiguration config)
    {
        _scopes = scopes;
        _log = log;
        _interval = TimeSpan.FromSeconds(config.GetValue("Lifecycle:IntervalSeconds", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Campaign lifecycle tick failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTimeOffset.UtcNow;

        var starting = await db.Campaigns
            .Where(c => c.Status == CampaignStatus.Scheduled && c.StartsAt <= now)
            .ToListAsync(ct);
        foreach (var c in starting)
        {
            c.Status = CampaignStatus.Live;
            c.UpdatedAt = now;
            _log.LogInformation("Campaign {CampaignId} is now live", c.Id);
        }

        var ending = await db.Campaigns
            .Where(c => c.Status == CampaignStatus.Live && c.EndsAt <= now)
            .ToListAsync(ct);
        foreach (var c in ending)
        {
            c.Status = CampaignStatus.Ended;
            c.UpdatedAt = now;
            await publish.Publish(new CampaignEnded(c.Id, now), ct);
            _log.LogInformation("Campaign {CampaignId} ended", c.Id);
        }

        await db.SaveChangesAsync(ct);
    }
}
