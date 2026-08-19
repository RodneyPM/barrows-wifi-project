using MassTransit;

namespace BarrowsWifi.Api.Messaging;

/// <summary>
/// KES-212: when a campaign ends, generate its summary and email it to the client's admins.
/// Not finished by Kestrel before handover.
/// </summary>
public class CampaignEndedConsumer : IConsumer<CampaignEnded>
{
    private readonly ILogger<CampaignEndedConsumer> _log;

    public CampaignEndedConsumer(ILogger<CampaignEndedConsumer> log)
    {
        _log = log;
    }

    public Task Consume(ConsumeContext<CampaignEnded> context)
    {
        // TODO (KES-212): build the summary with ReportService, store it in CampaignSummaries,
        // and email it to every ClientAdmin of the campaign's organisation via IReportMailer.
        _log.LogInformation("CampaignEnded received for campaign {CampaignId} - summary not implemented yet",
            context.Message.CampaignId);
        return Task.CompletedTask;
    }
}
