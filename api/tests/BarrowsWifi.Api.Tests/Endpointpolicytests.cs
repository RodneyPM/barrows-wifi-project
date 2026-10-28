using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BarrowsWifi.Api.Tests;

/// <summary>
/// Guard rails that apply to every endpoint, including ones added later, so a new route cannot
/// quietly ship without authorisation the way the report endpoint and campaign PUT did.
/// </summary>
public class EndpointPolicyTests : IClassFixture<ApiFactory>
{
    private static readonly string[] WriteMethods = { "POST", "PUT", "PATCH", "DELETE" };
    private readonly List<RouteEndpoint> _endpoints;

    public EndpointPolicyTests(ApiFactory factory) =>
        _endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    [Fact]
    public void Every_endpoint_requires_authorisation_unless_explicitly_public()
    {
        var unprotected = _endpoints
            .Where(e => !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            // Portal endpoints use the shared API key filter instead of a signed-in user.
            .Where(e => !(e.RoutePattern.RawText ?? "").StartsWith("/api/portal"))
            .Select(Describe)
            .ToList();

        Assert.True(unprotected.Count == 0, "Endpoints with no authorisation: " + string.Join(", ", unprotected));
    }

    [Fact]
    public void Campaign_write_endpoints_require_the_edit_policy()
    {
        var offenders = _endpoints
            .Where(e => (e.RoutePattern.RawText ?? "").StartsWith("/api/campaigns"))
            .Where(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>())
                .Any(m => WriteMethods.Contains(m)))
            .Where(e => !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "CanEditCampaigns"))
            .Select(Describe)
            .ToList();

        Assert.True(offenders.Count == 0, "Write endpoints missing CanEditCampaigns: " + string.Join(", ", offenders));
    }

    private static string Describe(RouteEndpoint e) =>
        $"{string.Join("/", e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>())} {e.RoutePattern.RawText}";
}