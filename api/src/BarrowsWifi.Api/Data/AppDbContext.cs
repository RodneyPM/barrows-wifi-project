using BarrowsWifi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignOutlet> CampaignOutlets => Set<CampaignOutlet>();
    public DbSet<ConnectionEvent> ConnectionEvents => Set<ConnectionEvent>();
    public DbSet<CampaignSummary> CampaignSummaries => Set<CampaignSummary>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Schema is owned by the SQL scripts in /api/db/migrations. The model mirrors it.
        b.Entity<Organisation>().ToTable("Organisations");

        b.Entity<User>().ToTable("Users");

        b.Entity<Outlet>(e =>
        {
            e.ToTable("Outlets");
            e.Property(x => x.StpNumber).HasColumnType("char(6)");
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
        });

        b.Entity<Campaign>(e =>
        {
            e.ToTable("Campaigns");
            e.Property(x => x.BudgetZar).HasPrecision(12, 2);
            e.HasMany(x => x.Outlets).WithOne().HasForeignKey(x => x.CampaignId);
        });

        b.Entity<CampaignOutlet>(e =>
        {
            e.ToTable("CampaignOutlets");
            e.HasKey(x => new { x.CampaignId, x.OutletId });
            e.HasOne(x => x.Outlet).WithMany().HasForeignKey(x => x.OutletId);
        });

        b.Entity<ConnectionEvent>(e =>
        {
            e.ToTable("ConnectionEvents");
            e.Property(x => x.ReceivedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        });

        b.Entity<CampaignSummary>().ToTable("CampaignSummaries");
        b.Entity<SentEmail>().ToTable("SentEmails");
    }
}
