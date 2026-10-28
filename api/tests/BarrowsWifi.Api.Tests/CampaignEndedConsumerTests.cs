using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Domain;
using BarrowsWifi.Api.Infrastructure;
using BarrowsWifi.Api.Messaging;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BarrowsWifi.Api.Tests;

public class CampaignEndedConsumerTests
{
    private const string Ops = "ops@ubuntubank.test";

    // ---- redelivery and existing delivery state

    [Fact]
    public async Task DuplicateMessage_DoesNotResendUnknownDelivery()
    {
        await using var sqlite = await SqliteDb.CreateAsync();
        var db = sqlite.Db;
        await SeedEndedCampaignAsync(db, withSummary: true, Ops);

        // Simulate the first attempt timing out after the relay sent.
        db.CampaignEmailDeliveries.Add(new CampaignEmailDelivery
        {
            CampaignId = 3,
            ToAddress = Ops,
            Status = CampaignEmailDeliveryStatus.Unknown,
            CreatedAt = DateTimeOffset.UtcNow,
            AttemptedAt = DateTimeOffset.UtcNow,
            LastError = "Mail relay did not acknowledge the message in time."
        });
        await db.SaveChangesAsync();

        var mailer = new Mock<IReportMailer>(MockBehavior.Strict);
        var consumer = CreateConsumer(db, mailer.Object);

        await consumer.Consume(ContextFor(3));
        await consumer.Consume(ContextFor(3));

        Assert.Single(await db.CampaignSummaries.Where(s => s.CampaignId == 3).ToListAsync());
        var delivery = await db.CampaignEmailDeliveries.SingleAsync(d => d.ToAddress == Ops);
        Assert.Equal(CampaignEmailDeliveryStatus.Unknown, delivery.Status);
        VerifySends(mailer, Ops, Times.Never());
    }

    [Fact]
    public async Task SuccessfulDelivery_DuplicateMessageDoesNotResendEmail()
    {
        await using var sqlite = await SqliteDb.CreateAsync();
        var db = sqlite.Db;
        await SeedEndedCampaignAsync(db, withSummary: true, Ops);

        db.CampaignEmailDeliveries.Add(new CampaignEmailDelivery
        {
            CampaignId = 3,
            ToAddress = Ops,
            Status = CampaignEmailDeliveryStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var mailer = new Mock<IReportMailer>(MockBehavior.Strict);
        mailer
            .Setup(m => m.SendAsync(
                Ops,
                It.Is<string>(s => s.Contains("Save Smart")),
                It.Is<string>(b => b.Contains("3559") && b.Contains("2967") && b.Contains("1098")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = CreateConsumer(db, mailer.Object);

        await consumer.Consume(ContextFor(3));   // first delivery sends
        await consumer.Consume(ContextFor(3));   // duplicate message does not

        var delivery = await db.CampaignEmailDeliveries.SingleAsync(d => d.ToAddress == Ops);
        Assert.Equal(CampaignEmailDeliveryStatus.Sent, delivery.Status);
        Assert.NotNull(delivery.AttemptedAt);
        Assert.NotNull(delivery.CompletedAt);
        Assert.Null(delivery.LastError);
        VerifySends(mailer, Ops, Times.Once());
    }

    [Fact]
    public async Task CampaignEnded_SendsSummaryOnlyToClientAdminsInCampaignOrganisation()
    {
        await using var sqlite = await SqliteDb.CreateAsync();
        var db = sqlite.Db;
        await SeedEndedCampaignAsync(db, withSummary: true, "admin1@ubuntubank.test", "admin2@ubuntubank.test");

        // People who must NOT get it: an admin of another organisation and a non-admin in this one.
        db.Organisations.Add(new Organisation { Id = 2, Name = "Other Organisation" });
        db.Users.AddRange(
            new User { Id = 100, Email = "otheradmin@otherorg.test", OrganisationId = 2, Role = Roles.ClientAdmin },
            new User { Id = 101, Email = "user@ubuntubank.test", OrganisationId = 1, Role = "User" });
        await db.SaveChangesAsync();

        var mailer = new Mock<IReportMailer>(MockBehavior.Strict);
        mailer
            .Setup(m => m.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateConsumer(db, mailer.Object).Consume(ContextFor(3));

        VerifySends(mailer, "admin1@ubuntubank.test", Times.Once());
        VerifySends(mailer, "admin2@ubuntubank.test", Times.Once());
        VerifySends(mailer, "otheradmin@otherorg.test", Times.Never());
        VerifySends(mailer, "user@ubuntubank.test", Times.Never());
        Assert.Equal(2, await db.CampaignEmailDeliveries
            .CountAsync(d => d.CampaignId == 3 && d.Status == CampaignEmailDeliveryStatus.Sent));
    }

    // ---- the ambiguous relay failure: the heart of the ticket

    [Fact]
    public Task RelayTimeoutAfterSending_IsMarkedUnknown_AndRedeliveryDoesNotResend() =>
        AssertAmbiguousFailureIsNeverResent(
            new TimeoutException("Mail relay did not acknowledge the message in time."));

    // An HTTP client reports a timeout as TaskCanceledException, which IS an OperationCanceledException.
    // It must be treated as "outcome unknown", not mistaken for the app shutting down.
    [Fact]
    public Task HttpStyleTimeout_TaskCanceledException_IsAlsoMarkedUnknown() =>
        AssertAmbiguousFailureIsNeverResent(
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout elapsing."));

    private static async Task AssertAmbiguousFailureIsNeverResent(Exception relayFailure)
    {
        await using var sqlite = await SqliteDb.CreateAsync();
        var db = sqlite.Db;
        await SeedEndedCampaignAsync(db, withSummary: true, Ops);

        var mailer = new Mock<IReportMailer>(MockBehavior.Strict);
        mailer
            .Setup(m => m.SendAsync(Ops, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(relayFailure);

        var consumer = CreateConsumer(db, mailer.Object);

        await consumer.Consume(ContextFor(3));   // must NOT throw: a timeout is not a reason to retry
        await consumer.Consume(ContextFor(3));   // the redelivered message

        var delivery = await db.CampaignEmailDeliveries.SingleAsync(d => d.ToAddress == Ops);
        Assert.Equal(CampaignEmailDeliveryStatus.Unknown, delivery.Status);
        Assert.Equal(relayFailure.Message, delivery.LastError);
        Assert.Null(delivery.CompletedAt);
        VerifySends(mailer, Ops, Times.Once());   // tried once; never sent a second copy
    }

    // ---- an explicit refusal is safe to retry

    [Fact]
    public async Task RefusedEmail_IsMarkedFailed_RetriedOnRedelivery_AndEndsUpSentOnce()
    {
        await using var sqlite = await SqliteDb.CreateAsync();
        var db = sqlite.Db;
        await SeedEndedCampaignAsync(db, withSummary: true, "admin1@ubuntubank.test", "admin2@ubuntubank.test");

        var mailer = new Mock<IReportMailer>(MockBehavior.Strict);
        // admin1: refused the first time (nothing sent), accepted the second time.
        mailer
            .SetupSequence(m => m.SendAsync("admin1@ubuntubank.test", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MailRejectedException("Relay refused the message."))
            .Returns(Task.CompletedTask);
        mailer
            .Setup(m => m.SendAsync("admin2@ubuntubank.test", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = CreateConsumer(db, mailer.Object);

        // First delivery: admin2 gets theirs, admin1 is refused, so the consumer throws and the bus will retry.
        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Consume(ContextFor(3)));
        var refused = await db.CampaignEmailDeliveries.SingleAsync(d => d.ToAddress == "admin1@ubuntubank.test");
        Assert.Equal(CampaignEmailDeliveryStatus.Failed, refused.Status);

        // The bus retry: only admin1 is attempted again.
        await consumer.Consume(ContextFor(3));

        Assert.All(await db.CampaignEmailDeliveries.ToListAsync(),
            d => Assert.Equal(CampaignEmailDeliveryStatus.Sent, d.Status));
        VerifySends(mailer, "admin1@ubuntubank.test", Times.Exactly(2));   // refused once, then accepted
        VerifySends(mailer, "admin2@ubuntubank.test", Times.Once());
    }

    // ---- the summary is built from the corrected report

    [Fact]
    public async Task CampaignEnded_BuildsSummaryFromTheCampaignsOwnEvents_AndStoresItOnce()
    {
        // In-memory provider: no recipients means no ExecuteUpdate, so it is not needed here.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var start = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(2));
        var end = start.AddDays(30);
        var organisation = new Organisation { Id = 1, Name = "Ubuntu Bank" };

        db.Organisations.Add(organisation);
        db.Outlets.Add(new Outlet
        {
            Id = 1, StpNumber = "835903", Name = "Test Tavern", Province = "Gauteng",
            CreatedAt = start, UpdatedAt = start
        });
        db.Campaigns.Add(new Campaign
        {
            Id = 3, Name = "Save Smart", OrganisationId = 1, Organisation = organisation,
            Status = CampaignStatus.Ended, StartsAt = start, EndsAt = end, BudgetZar = 1000m,
            CreatedAt = start, UpdatedAt = start,
            Outlets = { new CampaignOutlet { OutletId = 1 } }
        });
        db.ConnectionEvents.AddRange(
            Event(3, EventTypes.AdComplete, "dev-a", start.AddDays(3)),
            Event(3, EventTypes.AdComplete, "dev-a", start.AddDays(4)),
            Event(3, EventTypes.AdComplete, "dev-b", start.AddDays(5)),
            Event(3, EventTypes.SurveyComplete, "dev-a", start.AddDays(5)),
            // Traps: another campaign's event at the same outlet, and one exactly at the exclusive end.
            Event(4, EventTypes.AdComplete, "dev-c", start.AddDays(4)),
            Event(3, EventTypes.AdComplete, "dev-d", end));
        await db.SaveChangesAsync();

        var mailer = new Mock<IReportMailer>(MockBehavior.Strict);   // no ClientAdmin, so nothing may be sent
        var consumer = CreateConsumer(db, mailer.Object);

        await consumer.Consume(ContextFor(3));
        await consumer.Consume(ContextFor(3));   // redelivery reuses the stored summary

        var summary = Assert.Single(await db.CampaignSummaries.Where(s => s.CampaignId == 3).ToListAsync());
        Assert.Equal((3, 2, 1), (summary.Impressions, summary.UniqueUsers, summary.SurveyCompletes));

        // ...and it is exactly what the corrected report says.
        var report = await new ReportService(db).BuildForSystemAsync(3, CancellationToken.None);
        Assert.NotNull(report);
        Assert.Equal((report!.Impressions, report.UniqueUsers, report.SurveyCompletes),
            (summary.Impressions, summary.UniqueUsers, summary.SurveyCompletes));

        Assert.Empty(await db.CampaignEmailDeliveries.ToListAsync());
    }

    // ---- helpers

    /// <summary>A real SQLite database (it supports ExecuteUpdate, unlike the in-memory provider).</summary>
    private sealed class SqliteDb : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }

        private SqliteDb(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<SqliteDb> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteDb(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    /// <summary>Ubuntu Bank (org 1) with an ended campaign 3, the given ClientAdmins and, optionally, a stored summary.</summary>
    private static async Task SeedEndedCampaignAsync(AppDbContext db, bool withSummary, params string[] adminEmails)
    {
        var organisation = new Organisation { Id = 1, Name = "Ubuntu Bank" };
        db.Organisations.Add(organisation);

        db.Campaigns.Add(new Campaign
        {
            Id = 3,
            Name = "Save Smart",
            OrganisationId = organisation.Id,
            Organisation = organisation,
            Status = CampaignStatus.Ended,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
            EndsAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var userId = 1;
        foreach (var email in adminEmails)
        {
            db.Users.Add(new User { Id = userId++, Email = email, OrganisationId = 1, Role = Roles.ClientAdmin });
        }

        if (withSummary)
        {
            db.CampaignSummaries.Add(new CampaignSummary
            {
                CampaignId = 3,
                GeneratedAt = DateTimeOffset.UtcNow,
                Impressions = 3559,
                UniqueUsers = 2967,
                SurveyCompletes = 1098
            });
        }

        await db.SaveChangesAsync();
    }

    private static ConnectionEvent Event(int campaignId, string type, string device, DateTimeOffset at) => new()
    {
        EventId = Guid.NewGuid(), OutletId = 1, CampaignId = campaignId, DeviceHash = device,
        EventType = type, OccurredAt = at, ReceivedAt = at
    };

    private static CampaignEndedConsumer CreateConsumer(AppDbContext db, IReportMailer mailer) =>
        new(db, new ReportService(db), mailer, Mock.Of<ILogger<CampaignEndedConsumer>>());

    private static ConsumeContext<CampaignEnded> ContextFor(int campaignId)
    {
        var context = new Mock<ConsumeContext<CampaignEnded>>();
        context.SetupGet(c => c.Message).Returns(new CampaignEnded(campaignId, DateTimeOffset.UtcNow));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private static void VerifySends(Mock<IReportMailer> mailer, string to, Times times) =>
        mailer.Verify(m => m.SendAsync(to, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), times);
}