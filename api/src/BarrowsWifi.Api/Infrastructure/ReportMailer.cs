using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;

namespace BarrowsWifi.Api.Infrastructure;

public interface IReportMailer
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>
/// Local stand-in for the production mail relay. Every "sent" email is written to dbo.SentEmails.
///
/// It behaves like the real relay does on a bad day: roughly one call in four the relay accepts
/// the message but the acknowledgement times out, so the caller sees an exception even though
/// the email went out.
/// </summary>
public class FakeReportMailer : IReportMailer
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<FakeReportMailer> _log;
    private readonly double _ackFailureRate;

    public FakeReportMailer(IServiceScopeFactory scopes, ILogger<FakeReportMailer> log, IConfiguration config)
    {
        _scopes = scopes;
        _log = log;
        _ackFailureRate = config.GetValue("Mail:AckFailureRate", 0.25);
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        // The relay is a separate system: what it records is not part of the caller's transaction.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SentEmails.Add(new SentEmail { ToAddress = to, Subject = subject, Body = body, SentAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        _log.LogInformation("Email sent to {To}: {Subject}", to, subject);

        if (Random.Shared.NextDouble() < _ackFailureRate)
        {
            throw new TimeoutException("Mail relay did not acknowledge the message in time.");
        }
    }
}
