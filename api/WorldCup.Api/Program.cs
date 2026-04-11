using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WorldCup.Api.Data;
using WorldCup.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var matchesJsonPath = ResolveMatchesJsonPath(builder.Environment);
builder.Services.AddSingleton(MatchSchedule.LoadFromJson(matchesJsonPath));

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Default database connection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(defaultConnectionString));

builder.Services.AddHostedService<SqliteBackupService>();

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

RestoreDatabaseFromBackup(defaultConnectionString, builder.Configuration, builder.Environment);

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
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
    if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
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

static void RestoreDatabaseFromBackup(string connectionString, IConfiguration configuration, IWebHostEnvironment environment)
{
    var localDatabasePath = SqliteBackupPaths.ResolveLocalDatabasePath(connectionString, environment.ContentRootPath);
    if (File.Exists(localDatabasePath))
    {
        return;
    }

    var backupDatabasePath = Path.Combine("/mnt/backup", "worldcup.db");
    if (!File.Exists(backupDatabasePath))
    {
        return;
    }

    var localDirectory = Path.GetDirectoryName(localDatabasePath);
    if (!string.IsNullOrWhiteSpace(localDirectory))
    {
        Directory.CreateDirectory(localDirectory);
    }

    File.Copy(backupDatabasePath, localDatabasePath);
}
