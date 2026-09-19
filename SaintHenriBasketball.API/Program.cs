using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.API.Authorization;
using System.Threading.RateLimiting;
using Resend;
using SaintHenriBasketball.Application.Extensions;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using SaintHenriBasketball.Infrastructure.Jobs;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Reflection;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Rate limiting for auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // Signing up is not something a person does repeatedly. The shared "auth" budget of ten a
    // minute let a bot create a hundred accounts in a few days, each one sending a confirmation
    // email from the club's domain to an address the bot chose — the club's mail reputation is
    // the thing being spent here, so the budget is per hour, not per minute.
    options.AddPolicy("register", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));

    // The second factor needs a tighter budget than a password does. A TOTP code has a million
    // values and about three are live at once, so with the pending token valid for 15 minutes the
    // "auth" allowance of 10 a minute leaves the factor guessable. This does not.
    options.AddPolicy("2fa", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    options.RejectionStatusCode = 429;
});

builder.Services.AddSingleton(builder.Environment);


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
// Configure Swagger to work with API versioning
builder.Services.AddSwaggerGen(options =>
{
    // Get all API version descriptions
    var provider = builder.Services.BuildServiceProvider()
        .GetRequiredService<IApiVersionDescriptionProvider>();

    // Create a swagger document for each API version
    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerDoc(
            description.GroupName,
            new OpenApiInfo
            {
                Title = $"Saint Henri Basketball API {description.ApiVersion}",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "This API version has been deprecated."
                    : "API for managing basketball sessions, attendance, and payments."
            });
    }

    // Set the comments path for the Swagger JSON and UI
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add security definitions
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
});

// Add API version explorer to enable Swagger to understand versions
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]!))
        };
        options.Events = new JwtBearerEvents
        {
            // A signed token stays valid until it expires, so re-check the account: deactivation and
            // admin removal take effect within a minute instead of at expiry.
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var raw = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (principal is null || !Guid.TryParse(raw, out var userId))
                {
                    context.Fail("Invalid token");
                    return;
                }
                var services = context.HttpContext.RequestServices;
                var snapshot = await SaintHenriBasketball.API.Filters.AuthUserCache.GetAsync(
                    services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    services.GetRequiredService<SaintHenriBasketball.Domain.Interfaces.Repositories.IUserRepository>(),
                    userId);
                var reason = SaintHenriBasketball.Application.Helpers.TokenUserCheck.Evaluate(
                    snapshot,
                    principal.IsInRole("Admin"),
                    principal.FindFirst(SaintHenriBasketball.Application.Helpers.StaffAccess.ClaimType)?.Value);
                if (reason != null) context.Fail(reason);
            }
        };
    });

// Volunteer staff policies: admin, or the matching staff role while volunteer-roles is on.
builder.Services.AddAuthorization(options => options.AddStaffRolePolicies());
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, StaffRoleAuthorizationHandler>();

// Add memory caching
builder.Services.AddMemoryCache();

// Enable caching profiles in controllers
builder.Services.AddControllers(options =>
{
    options.CacheProfiles.Add("Default30",
        new CacheProfile { Duration = 30 });
    options.CacheProfiles.Add("Default60",
        new CacheProfile { Duration = 60 });
    // Every successful admin-only change gets an audit entry unless the endpoint wrote its own.
    options.Filters.Add<AdminMutationAuditFilter>();
});


// Add Resend email service
builder.Services.AddOptions();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["Resend:ApiKey"]!;
});
builder.Services.AddTransient<IResend, ResendClient>();

// Brevo SMS (used when Sms:Provider = Brevo)
builder.Services.AddHttpClient<SaintHenriBasketball.Application.Services.Implementations.BrevoSmsService>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));

// Configure Stripe
builder.Services.Configure<SaintHenriBasketball.Application.Settings.StripeSettings>(
    builder.Configuration.GetSection(SaintHenriBasketball.Application.Settings.StripeSettings.SectionName));
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Add application services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Quartz.NET job scheduling
builder.Services.AddQuartzJobs(startScheduler: !(builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("LocalTesting:DisableScheduledJobs")));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        corsPolicyBuilder =>
        {
            corsPolicyBuilder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   // Lets the app read the file name of downloads (CSV exports, invoices).
                   .WithExposedHeaders("Content-Disposition");
        });
});

var app = builder.Build();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("LocalTesting:UseModelSchema"))
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Seed feature flags (adds new keys with Enabled=false; preserves existing toggle state)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var featureFlagService = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        await featureFlagService.SeedDefaultsAsync(FeatureFlagDefinitions.All);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to seed feature flag defaults at startup.");
    }
}

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(c =>
//    {
//        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Basketball Training API v1");
//    });
//}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    // Get all API version descriptions
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    // Create a swagger endpoint for each API version
    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            $"Saint Henri Basketball API {description.GroupName}");
    }
});

// Enable request body buffering for Stripe webhook signature verification
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/stripe"))
    {
        context.Request.EnableBuffering();
    }
    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();

// Use CORS before Users
app.UseCors("AllowAll");

// Add authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

// Enforce 2FA challenge when JWT carries the pending claim
app.UseMiddleware<SaintHenriBasketball.API.Filters.TwoFactorPendingMiddleware>();

// Quartz jobs are started automatically by the hosted service

// In the HTTP pipeline
app.UseResponseCaching();

app.MapControllers();

app.Run();
