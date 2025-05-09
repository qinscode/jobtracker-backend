using System.Text;
using System.Text.Json.Serialization;
using JobTracker.Data;
using JobTracker.Repositories;
using JobTracker.Repositories.Interfaces;
using JobTracker.Services;
using JobTracker.Services.BackgroundServices;
using JobTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Load configuration from app settings.json and environment variables
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddEnvironmentVariables()
    .Build();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(builder.Configuration.GetValue("API_PORT", 5051));
});

// Configure JWT authentication
builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    })
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"] ??
                     throw new InvalidOperationException("JWT key is not configured");
        var key = Encoding.UTF8.GetBytes(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DefaultIssuer",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "DefaultAudience",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    })
    .AddGoogle(googleOptions =>
    {
        var clientId = builder.Configuration["Authentication:Google:ClientId"] ??
                       throw new InvalidOperationException("Google Client ID is not configured");
        var clientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ??
                           throw new InvalidOperationException("Google Client Secret is not configured");

        googleOptions.ClientId = clientId;
        googleOptions.ClientSecret = clientSecret;
    });

// Add DbContext
builder.Services.AddDbContext<JobTrackerContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IUserJobRepository, UserJobRepository>();
builder.Services.AddScoped<IUserEmailConfigRepository, UserEmailConfigRepository>();
builder.Services.AddScoped<IAnalyzedEmailRepository, AnalyzedEmailRepository>();

// Register email services
builder.Services.AddHttpClient<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailAnalysisService, EmailAnalysisService>();

// Register services
builder.Services.AddScoped<IJobMatchingService, JobMatchingService>();

// 添加后台服务，直接设置60分钟间隔
builder.Services.AddHostedService<EmailAnalysisBackgroundService>();
builder.Services.Configure<EmailAnalysisBackgroundServiceOptions>(options =>
{
    options.IntervalMinutes = 60; // 直接设置为60分钟
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection(); // 强制使用HTTPS

// Enable CORS
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


app.Run();