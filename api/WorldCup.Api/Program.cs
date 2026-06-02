using System.Text;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using WorldCup.Api.Data;
using WorldCup.Api.Features;
using WorldCup.Api.Hubs;
using WorldCup.Api.Logging;
using WorldCup.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Send Warning/Error/Critical logs to Slack when SLACK_WEBHOOK_URL is configured.
var slackWebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");
if (!string.IsNullOrWhiteSpace(slackWebhookUrl))
{
    builder.Logging.AddSlack(options =>
    {
        options.WebhookUrl = slackWebhookUrl;
        options.MinimumLevel = LogLevel.Error;
    });
}

// Optionally pull configuration (including feature flags) from Azure App Configuration.
// When APP_CONFIGURATION_ENDPOINT is set we authenticate with the workload's managed identity
// (or DefaultAzureCredential locally), so no connection string / secret is needed.
// When the variable is not set we fall back to appsettings.json — which keeps local dev simple
// and means tests don't need any Azure setup.
var appConfigEndpoint = builder.Configuration["AppConfiguration:Endpoint"]
    ?? Environment.GetEnvironmentVariable("APP_CONFIGURATION_ENDPOINT");

if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
            .UseFeatureFlags(featureFlagOptions =>
            {
                // Refresh feature flags every 30 seconds so toggles can be flipped without redeploy.
                featureFlagOptions.SetRefreshInterval(TimeSpan.FromSeconds(30));
            });
    });

    builder.Services.AddAzureAppConfiguration();
}

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

// Feature flag system. The BettingGroupFilter enables flags for a configured set of group ids.
// Configure flags under the "FeatureManagement" section of appsettings.json.
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<BettingGroupFilter>();

var matchesJsonPath = ResolveMatchesJsonPath(builder.Environment);

if (!File.Exists(matchesJsonPath))
{
    var seedSource = Path.Combine(AppContext.BaseDirectory, "data", "matches.json");
    if (File.Exists(seedSource))
    {
        var dir = Path.GetDirectoryName(matchesJsonPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.Copy(seedSource, matchesJsonPath, overwrite: true);
    }
}

builder.Services.AddSingleton(new MatchScheduleProvider(matchesJsonPath));
builder.Services.AddSingleton<TeamCodeMapper>();
builder.Services.AddSingleton<IOptions<MatchFileWriterOptions>>(
    Options.Create(new MatchFileWriterOptions { JsonPath = matchesJsonPath }));
builder.Services.AddSingleton<MatchFileWriter>();
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<ResultAnnouncementService>();
builder.Services.AddHttpClient<Wc2026ApiClient>();
builder.Services.AddHostedService<ResultFetcherService>();

// Værvarsel (Open-Meteo). MemoryCache holder svar i ~3t per stadion+dato slik at
// klienten kan kalle endepunktet fritt uten å belaste tredjepart.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<WeatherService>();

// Lag-statistikk og innbyrdes oppgjør (H2H) for matchdetalj-siden. Service'en
// leser fra seed-fila data/teamStats.json. Hvis du senere ønsker å koble på et
// betalt API: implementer IExternalTeamStatsClient og bytt registreringen under.
// Merge-logikken i TeamStatsService håndterer både hel-overskriving og delvis
// utfylling (ekstern data vinner per felt, seed fyller hull).
builder.Services.AddSingleton<IExternalTeamStatsClient, NoopExternalTeamStatsClient>();
builder.Services.AddSingleton<TeamStatsService>();

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Default database connection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(defaultConnectionString));

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() is { Length: > 0 } configuredOrigins
    ? configuredOrigins
    : ["http://localhost:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteClient", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT signing key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Allow JWT to be sent via query string for SignalR WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHostedService<DatabaseMigrationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ViteClient");
if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
{
    // Triggers periodic feature-flag refresh from Azure App Configuration on incoming requests.
    app.UseAzureAppConfiguration();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapFallbackToFile("index.html");

app.Run();

static string ResolveMatchesJsonPath(IWebHostEnvironment environment)
{
    var configuredPath = Environment.GetEnvironmentVariable("MATCHES_JSON_PATH");
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        return configuredPath;
    }

    var contentRootPath = Path.Combine(environment.ContentRootPath, "data", "matches.json");
    if (File.Exists(contentRootPath))
    {
        return contentRootPath;
    }

    return Path.Combine(environment.ContentRootPath, "..", "..", "src", "data", "matches.json");
}
