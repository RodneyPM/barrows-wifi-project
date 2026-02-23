using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BarrowsWifi.Api.Endpoints;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public record UserDto(int Id, string Email, string Role, int OrganisationId, string OrganisationName);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/token", async (LoginRequest req, AppDbContext db, TokenService tokens, CancellationToken ct) =>
        {
            var user = await db.Users.Include(u => u.Organisation)
                .FirstOrDefaultAsync(u => u.Email == req.Email.Trim().ToLower(), ct);
            if (user is null || !PasswordHashing.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();

            var (token, expires) = tokens.Issue(user);
            return Results.Ok(new LoginResponse(token, expires,
                new UserDto(user.Id, user.Email, user.Role, user.OrganisationId, user.Organisation?.Name ?? "")));
        }).AllowAnonymous();

        group.MapGet("/me", async (System.Security.Claims.ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var me = CurrentUser.From(principal);
            var org = await db.Organisations.FindAsync(new object[] { me.OrganisationId }, ct);
            return Results.Ok(new UserDto(me.Id, me.Email, me.Role, me.OrganisationId, org?.Name ?? ""));
        }).RequireAuthorization();
    }
}
