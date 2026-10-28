namespace BarrowsWifi.Api.Infrastructure;

/// <summary>
/// The mail relay definitively refused the message, so nothing was sent and trying again is safe.
/// Any OTHER exception from <see cref="IReportMailer"/> (a timeout, a dropped connection) means the outcome
/// is unknown: the relay may well have sent it, so callers must not blindly send it again.
/// </summary>
public class MailRejectedException : Exception
{
    public MailRejectedException(string message) : base(message) { }
}