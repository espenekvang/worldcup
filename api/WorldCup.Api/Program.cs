using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using WorldCup.Api.Data;
using WorldCup.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
builder.Services.AddHttpClient<Wc2026ApiClient>();
builder.Services.AddHostedService<ResultFetcherService>();

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Default database connection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(defaultConnectionString));

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() is { Length: > 0 } configuredOrigins
    ? configuredOrigins
    : ["http://localhost:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteClient", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migrationLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

    const int maxRetries = 5;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            migrationLogger.LogInformation("Database migration completed successfully on attempt {Attempt}", attempt);
            break;
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 15 && attempt < maxRetries)
        {
            migrationLogger.LogWarning(
                "SQLite locking protocol error on migration attempt {Attempt}/{MaxRetries}, retrying in {Delay}s...",
                attempt, maxRetries, attempt * 2);
            Thread.Sleep(TimeSpan.FromSeconds(attempt * 2));
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 15)
        {
            migrationLogger.LogWarning(
                ex,
                "SQLite locking protocol error persisted after {MaxRetries} attempts. "
                + "Checking if migrations are already applied...",
                maxRetries);

            var pending = dbContext.Database.GetPendingMigrations().ToList();
            if (pending.Count == 0)
            {
                migrationLogger.LogInformation("All migrations already applied — continuing startup");
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot apply {pending.Count} pending migration(s) due to SQLite locking error: {string.Join(", ", pending)}",
                    ex);
            }
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ViteClient");
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllers();
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
