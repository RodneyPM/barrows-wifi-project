namespace BarrowsWifi.Api.Domain;

public class Organisation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Client";
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.ClientViewer;
    public int OrganisationId { get; set; }
    public Organisation? Organisation { get; set; }
}

public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string ClientAdmin = "ClientAdmin";
    public const string ClientViewer = "ClientViewer";
}

public class Outlet
{
    public int Id { get; set; }
    public string StpNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string Province { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? OwnerName { get; set; }
    public string ApStatus { get; set; } = "Online";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class CampaignStatus
{
    public const string Draft = "Draft";
    public const string Scheduled = "Scheduled";
    public const string Live = "Live";
    public const string Ended = "Ended";
}

public class Campaign
{
    public int Id { get; set; }
    public int OrganisationId { get; set; }
    public Organisation? Organisation { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = CampaignStatus.Draft;

    /// <summary>Inclusive start of the flight.</summary>
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Exclusive end of the flight.</summary>
    public DateTimeOffset EndsAt { get; set; }

    public decimal BudgetZar { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<CampaignOutlet> Outlets { get; set; } = new();
}

public class CampaignOutlet
{
    public int CampaignId { get; set; }
    public int OutletId { get; set; }
    public Outlet? Outlet { get; set; }
}

public class ConnectionEvent
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public int OutletId { get; set; }
    public int? CampaignId { get; set; }
    public string DeviceHash { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string? Msisdn { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public static class EventTypes
{
    public const string Connect = "connect";
    public const string PortalView = "portal_view";
    public const string AdComplete = "ad_complete";
    public const string SurveyComplete = "survey_complete";

    public static readonly string[] All = { Connect, PortalView, AdComplete, SurveyComplete };
}

public class CampaignSummary
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public int Impressions { get; set; }
    public int UniqueUsers { get; set; }
    public int SurveyCompletes { get; set; }
}

public class SentEmail
{
    public int Id { get; set; }
    public string ToAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}
