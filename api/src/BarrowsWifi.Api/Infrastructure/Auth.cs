using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BarrowsWifi.Api.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BarrowsWifi.Api.Infrastructure;

public static class ClaimNames
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string OrganisationId = "org";
}

/// <summary>The signed-in user, read from the JWT.</summary>
public sealed record CurrentUser(int Id, string Email, string Role, int OrganisationId)
{
    public bool IsPlatformAdmin => Role == Roles.PlatformAdmin;

    public static CurrentUser From(ClaimsPrincipal principal) => new(
        int.Parse(principal.FindFirst(ClaimNames.UserId)!.Value),
        principal.FindFirst(ClaimNames.Email)!.Value,
        principal.FindFirst(ClaimNames.Role)!.Value,
        int.Parse(principal.FindFirst(ClaimNames.OrganisationId)!.Value));
}

public static class OrgScoping
{
    /// <summary>Limits campaigns to the caller's organisation. Platform admins see everything.</summary>
    public static IQueryable<Campaign> VisibleTo(this IQueryable<Campaign> query, CurrentUser user) =>
        user.IsPlatformAdmin ? query : query.Where(c => c.OrganisationId == user.OrganisationId);
}

public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 60;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public class TokenService
{
    private readonly JwtOptions _options;

    public TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.LifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(_options.Key(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimNames.UserId, user.Id.ToString()),
                new Claim(ClaimNames.Email, user.Email),
                new Claim(ClaimNames.Role, user.Role),
                new Claim(ClaimNames.OrganisationId, user.OrganisationId.ToString()),
            }),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

public static class PasswordHashing
{
    // Format: PBKDF2-SHA256$<iterations>$<base64 salt>$<base64 hash>
    public static string Hash(string password, int iterations = 100_000)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256") return false;
        var iterations = int.Parse(parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
