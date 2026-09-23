using System.Text;
using Ecosologic.Api.Configuration;
using Ecosologic.Api.Media;
using Ecosologic.Api.Security;
using Ecosologic.Infrastructure.Crm;
using Ecosologic.Infrastructure.Email;
using Ecosologic.Infrastructure.Jobs;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var jwtKey = builder.Configuration["Auth:JwtKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Auth:JwtKey é obrigatório. Defina a variável de ambiente Auth__JwtKey.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = "ecosologic",
        ValidateAudience = true,
        ValidAudience = "ecosologic",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddRateLimiter(options =>
{
    AuthRateLimiter.Configure(options);
    MediaRateLimiter.Configure(options);
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(DataProtectionKeys.Resolve(builder.Configuration)));

builder.Services.AddControllers();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
var connectionString = ConnectionString.Resolve(builder.Configuration);
builder.Services.AddDbContext<EcosologicDbContext>(options =>
    options.UseNpgsql(connectionString));

HangfireServiceRegistration.AddHangfireServices(builder.Services, builder.Configuration, connectionString);

var crmTimeZoneId = builder.Configuration["Crm:TimeZoneId"] ?? "America/Sao_Paulo";
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(crmTimeZoneId));
builder.Services.AddScoped<CrmNotificationService>();
builder.Services.AddScoped<ICrmNotificationSync>(sp => sp.GetRequiredService<CrmNotificationService>());
builder.Services.AddScoped<TariffCatalog>();
builder.Services.AddAneelTariffSource();
builder.Services.AddAneelTariffImport();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<ILeadEmailSender, LeadEmailSender>();

var mediaPath = builder.Configuration["Storage:MediaPath"] ?? "wwwroot/uploads";
var mediaRoot = Path.IsPathRooted(mediaPath) ? mediaPath : Path.Combine(builder.Environment.ContentRootPath, mediaPath);
builder.Services.AddSingleton<IMediaStorage>(new LocalMediaStorage(mediaRoot));
builder.Services.AddSingleton<IImageSanitizer, ImageSanitizer>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

var migrateMaxAttempts = int.TryParse(builder.Configuration["Database:MigrateMaxAttempts"], out var maxAttempts) && maxAttempts > 0
    ? maxAttempts
    : DatabaseMigrator.DefaultMaxAttempts;
var migrateBackoff = DatabaseMigrationSettings.ParseBackoff(builder.Configuration["Database:MigrateBackoffSeconds"]);

using (var scope = app.Services.CreateScope())
{
    var migrator = new DatabaseMigrator();
    await migrator.MigrateAsync(
        cancellationToken => scope.ServiceProvider.GetRequiredService<EcosologicDbContext>().Database.MigrateAsync(cancellationToken),
        migrateMaxAttempts,
        migrateBackoff,
        app.Lifetime.ApplicationStopping);
}

HangfireJobRegistration.RegisterAneelRetryFilter(builder.Configuration);

var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
HangfireJobRegistration.RegisterRecurringJobs(recurringJobManager, builder.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();
app.UseCors("frontend");

app.UseStaticFiles();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

HangfireDashboardConfiguration.MapDashboard(app);

app.MapControllers();

app.Run();
