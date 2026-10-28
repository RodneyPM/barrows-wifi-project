using BarrowsWifi.Api.Data;
using BarrowsWifi.Api.Endpoints;
using BarrowsWifi.Api.Infrastructure;
using BarrowsWifi.Api.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

var connectionString = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// ---- Auth
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.AddSingleton<TokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.Key(),
            ValidateIssuerSigningKey = true,
            // Portal tablets in some outlets have badly drifted clocks; disabled until we sort out NTP. (KES-198)
            ValidateLifetime = false,
            NameClaimType = ClaimNames.Email,
            RoleClaimType = ClaimNames.Role,
        };
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("CanEditCampaigns", p => p.RequireRole("PlatformAdmin", "ClientAdmin"));
});

// ---- Application services
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<IReportMailer, FakeReportMailer>();
builder.Services.AddHostedService<CampaignLifecycleService>();

// ---- Messaging. In production this is Azure Service Bus; locally the in-memory transport.
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CampaignEndedConsumer>();
    x.UsingInMemory((context, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Intervals(500, 2000, 5000));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply database scripts on startup (local + test environments).
var scriptsFolder = Path.Combine(AppContext.BaseDirectory, "db", "migrations");
// Integration tests host the API against an in-memory database and set the "Testing" environment.
if (!app.Environment.IsEnvironment("Testing"))
{
    await DbMigrator.RunAsync(connectionString, scriptsFolder, app.Logger);
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapCampaignEndpoints();
app.MapOutletEndpoints();
app.MapReportEndpoints();
app.MapPortalEndpoints();

app.Run();

public partial class Program { }
