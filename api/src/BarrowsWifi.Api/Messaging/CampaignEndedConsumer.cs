using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using BarrowsWifi.Api.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Messaging;

/// <summary>
/// KES-212: when a campaign ends, store its summary and email it to the client's admins.
///
/// The bus can deliver the same message twice and the mail relay can send an email yet fail to
/// acknowledge it, so this consumer is written to be safe to run repeatedly:
///   * one stored summary per campaign (unique index), reused by every run;
///   * each recipient's delivery is CLAIMED atomically before the mailer is called;
///   * a relay timeout means "may have been sent" (Unknown) and is never retried automatically;
///   * only an explicit refusal (MailRejectedException, nothing sent) is retried.
/// </summary>
public class CampaignEndedConsumer : IConsumer<CampaignEnded>
{
    private readonly AppDbContext _db;
    private readonly ReportService _reports;
    private readonly IReportMailer _mailer;
    private readonly ILogger<CampaignEndedConsumer> _log;

    public CampaignEndedConsumer(
        AppDbContext db,
        ReportService reports,
        IReportMailer mailer,
        ILogger<CampaignEndedConsumer> log)
    {
        _db = db;
        _reports = reports;
        _mailer = mailer;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CampaignEnded> context)
    {
        var ct = context.CancellationToken;
        var campaignId = context.Message.CampaignId;

        var campaign = await _db.Campaigns
            .Include(c => c.Organisation)
            .SingleOrDefaultAsync(c => c.Id == campaignId, ct);

        if (campaign is null)
        {
            _log.LogWarning("CampaignEnded received for missing campaign {CampaignId}", campaignId);
            return;
        }

        var summary = await GetOrCreateSummaryAsync(campaignId, ct);
        if (summary is null) return;

        var recipients = await _db.Users
            .Where(u => u.OrganisationId == campaign.OrganisationId && u.Role == Roles.ClientAdmin)
            .Select(u => u.Email)
            .Distinct()
            .ToListAsync(ct);

        var refused = 0;
        foreach (var recipient in recipients)
        {
            if (!await DeliverAsync(campaign, summary, recipient, ct)) refused++;
        }

        // Only an explicit refusal gets here (nothing was sent). Throwing makes the bus retry the message;
        // the retry touches only the Failed recipients because everyone else is Sent, Unknown or Sending.
        if (refused > 0)
        {
            throw new InvalidOperationException(
                $"Mail relay refused the summary for campaign {campaignId} for {refused} recipient(s); will retry those.");
        }
    }

    private async Task<CampaignSummary?> GetOrCreateSummaryAsync(int campaignId, CancellationToken ct)
    {
        // Reuse the stored snapshot on duplicate message deliveries.
        var existing = await _db.CampaignSummaries.SingleOrDefaultAsync(s => s.CampaignId == campaignId, ct);
        if (existing is not null) return existing;

        // No signed-in user here, so this is the trusted system path (see ReportService.BuildForSystemAsync).
        var report = await _reports.BuildForSystemAsync(campaignId, ct);
        if (report is null)
        {
            _log.LogWarning("Could not generate report for campaign {CampaignId}", campaignId);
            return null;
        }

        var summary = new CampaignSummary
        {
            CampaignId = campaignId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Impressions = report.Impressions,
            UniqueUsers = report.UniqueUsers,
            SurveyCompletes = report.SurveyCompletes
        };

        _db.CampaignSummaries.Add(summary);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A duplicate delivery of this message stored it first: use theirs.
            _db.Entry(summary).State = EntityState.Detached;
            return await _db.CampaignSummaries.SingleAsync(s => s.CampaignId == campaignId, ct);
        }

        _log.LogInformation(
            "Saved summary for campaign {CampaignId}: {Impressions} impressions, " +
            "{UniqueUsers} unique users, {SurveyCompletes} survey completes",
            campaignId, summary.Impressions, summary.UniqueUsers, summary.SurveyCompletes);

        return summary;
    }

    /// <returns>false only when the relay explicitly refused the email, so the caller should retry later.</returns>
    private async Task<bool> DeliverAsync(
        Campaign campaign, CampaignSummary summary, string recipient, CancellationToken ct)
    {
        var delivery = await _db.CampaignEmailDeliveries
            .SingleOrDefaultAsync(d => d.CampaignId == campaign.Id && d.ToAddress == recipient, ct);

        if (delivery is null)
        {
            delivery = new CampaignEmailDelivery
            {
                CampaignId = campaign.Id,
                ToAddress = recipient,
                Status = CampaignEmailDeliveryStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.CampaignEmailDeliveries.Add(delivery);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Another handler created the row at the same moment: it owns this recipient.
                _db.Entry(delivery).State = EntityState.Detached;
                _log.LogInformation(
                    "Another handler already created the delivery for campaign {CampaignId} to {Recipient}",
                    campaign.Id, recipient);
                return true;
            }
        }

        // Pending = never tried. Failed = the relay refused it, so nothing was sent and trying again is safe.
        // Sending, Sent and Unknown are never retried automatically.
        if (delivery.Status is not (CampaignEmailDeliveryStatus.Pending or CampaignEmailDeliveryStatus.Failed))
        {
            _log.LogInformation(
                "Skipping campaign {CampaignId} email to {Recipient}; delivery status is {Status}",
                campaign.Id, recipient, delivery.Status);
            return true;
        }

        // Atomically claim the delivery. Only the handler that changes exactly one row may send.
        var attemptedAt = DateTimeOffset.UtcNow;
        var claimed = await _db.CampaignEmailDeliveries
            .Where(d => d.Id == delivery.Id &&
                        (d.Status == CampaignEmailDeliveryStatus.Pending ||
                         d.Status == CampaignEmailDeliveryStatus.Failed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.Status, CampaignEmailDeliveryStatus.Sending)
                .SetProperty(d => d.AttemptedAt, attemptedAt), ct);

        if (claimed != 1)
        {
            _log.LogInformation(
                "Another worker already claimed campaign {CampaignId} email delivery to {Recipient}",
                campaign.Id, recipient);
            return true;
        }

        // ExecuteUpdate bypasses the change tracker, so re-read the row we just changed.
        await _db.Entry(delivery).ReloadAsync(ct);

        string outcome;
        string? error = null;
        try
        {
            await _mailer.SendAsync(recipient, BuildSubject(campaign), BuildBody(campaign, summary), ct);
            outcome = CampaignEmailDeliveryStatus.Sent;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The app is shutting down mid-send. Leave the row as Sending: we cannot know if it went out.
            throw;
        }
        catch (MailRejectedException ex)
        {
            outcome = CampaignEmailDeliveryStatus.Failed;
            error = Truncate(ex.Message);
            _log.LogWarning(ex,
                "Mail relay refused the summary for campaign {CampaignId} to {Recipient}; it will be retried",
                campaign.Id, recipient);
        }
        catch (Exception ex)
        {
            // Includes TimeoutException AND the TaskCanceledException an HTTP client throws on a timeout.
            // The relay may have sent the email before failing to acknowledge it, so do not send it again.
            outcome = CampaignEmailDeliveryStatus.Unknown;
            error = Truncate(ex.Message);
            _log.LogWarning(ex,
                "Delivery outcome is unknown for campaign {CampaignId} and recipient {Recipient}. " +
                "Automatic resend suppressed.",
                campaign.Id, recipient);
        }

        // Recorded OUTSIDE the try, so a failed save here is never mistaken for a mailer failure.
        // If this save fails the row stays Sending, which is never resent: at-most-once is preserved.
        delivery.Status = outcome;
        delivery.LastError = error;
        delivery.CompletedAt = outcome == CampaignEmailDeliveryStatus.Sent ? DateTimeOffset.UtcNow : null;
        await _db.SaveChangesAsync(ct);

        if (outcome == CampaignEmailDeliveryStatus.Sent)
        {
            _log.LogInformation("Campaign {CampaignId} summary emailed to {Recipient}", campaign.Id, recipient);
        }

        return outcome != CampaignEmailDeliveryStatus.Failed;
    }

    private static string BuildSubject(Campaign campaign) => $"Campaign summary: {campaign.Name}";

    private static string BuildBody(Campaign campaign, CampaignSummary summary) => $"""
        Campaign summary

        Campaign: {campaign.Name}
        Organisation: {campaign.Organisation?.Name ?? "Unknown"}
        Campaign ID: {campaign.Id}

        Impressions: {summary.Impressions}
        Unique users: {summary.UniqueUsers}
        Survey completes: {summary.SurveyCompletes}

        Summary generated at: {summary.GeneratedAt:O}
        """;

    private static string Truncate(string message) => message.Length <= 1000 ? message : message[..1000];
}