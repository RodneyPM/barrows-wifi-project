namespace BarrowsWifi.Api.Domain;

/// <summary>STP numbers are 6-digit store identifiers. Leading zeros are significant.</summary>
public static class StpNumber
{
    public static bool TryNormalise(string? input, out string stp)
    {
        stp = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (trimmed.Length > 6 || !trimmed.All(char.IsDigit)) return false;
        stp = trimmed.PadLeft(6, '0');
        return true;
    }
}

public static class CampaignRules
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [CampaignStatus.Draft] = new[] { CampaignStatus.Scheduled },
        [CampaignStatus.Scheduled] = new[] { CampaignStatus.Draft, CampaignStatus.Live },
        [CampaignStatus.Live] = new[] { CampaignStatus.Ended },
        [CampaignStatus.Ended] = Array.Empty<string>(),
    };

    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);
}
