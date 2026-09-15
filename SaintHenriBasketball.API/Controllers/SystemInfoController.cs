using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.API.Controllers;

/// What the running API is and how it's configured, for the admin settings page. Never returns secret values.
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/system-info")]
[ApiController]
[Authorize(Roles = "Admin")]
public class SystemInfoController(
    ApplicationDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<SystemInfoController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SystemInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemInfoDto>> Get()
    {
        var assembly = typeof(SystemInfoController).Assembly;
        var info = new SystemInfoDto
        {
            Environment = environment.EnvironmentName,
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString() ?? "unknown",
            BuiltAtUtc = string.IsNullOrEmpty(assembly.Location) ? null : System.IO.File.GetLastWriteTimeUtc(assembly.Location),
            ServerTimeUtc = DateTime.UtcNow,
            AppUrl = configuration["AppUrl"],
            EmailConfigured = !string.IsNullOrWhiteSpace(configuration["Resend:ApiKey"]),
            EmailSuppressed = configuration.GetValue<bool>("LocalTesting:SuppressEmail"),
            ScheduledJobsDisabled = configuration.GetValue<bool>("LocalTesting:DisableScheduledJobs"),
            StripeMode = StripeMode(configuration["Stripe:SecretKey"]),
            SmsProvider = string.IsNullOrWhiteSpace(configuration["Sms:Provider"]) ? "Log only" : configuration["Sms:Provider"]!,
        };

        try
        {
            info.DatabaseReachable = await db.Database.CanConnectAsync();
            if (info.DatabaseReachable)
            {
                info.ActivePlayers = await db.Users.CountAsync(u => !u.IsDeactivated && !u.IsAdmin);
                info.ActiveAdmins = await db.Users.CountAsync(u => !u.IsDeactivated && u.IsAdmin);
                info.AdminsWithTwoFactor = await db.Users.CountAsync(u => !u.IsDeactivated && u.IsAdmin && u.TwoFactorEnabled);
                info.FeatureFlagsEnabled = await db.FeatureFlags.CountAsync(f => f.Enabled);
                info.FeatureFlagsTotal = await db.FeatureFlags.CountAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "System info could not reach the database");
            info.DatabaseReachable = false;
        }

        return Ok(info);
    }

    /// Test or live from the key prefix only; the key itself never leaves the server.
    public static string StripeMode(string? secretKey) =>
        string.IsNullOrWhiteSpace(secretKey) ? "Not configured"
        : secretKey.StartsWith("sk_live_") || secretKey.StartsWith("rk_live_") ? "Live"
        : "Test";
}

public class SystemInfoDto
{
    public string Environment { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime? BuiltAtUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public string? AppUrl { get; set; }
    public bool DatabaseReachable { get; set; }
    public bool EmailConfigured { get; set; }
    /// Local testing: emails are logged instead of sent.
    public bool EmailSuppressed { get; set; }
    public bool ScheduledJobsDisabled { get; set; }
    public string StripeMode { get; set; } = "";
    public string SmsProvider { get; set; } = "";
    public int ActivePlayers { get; set; }
    public int ActiveAdmins { get; set; }
    public int AdminsWithTwoFactor { get; set; }
    public int FeatureFlagsEnabled { get; set; }
    public int FeatureFlagsTotal { get; set; }
}
