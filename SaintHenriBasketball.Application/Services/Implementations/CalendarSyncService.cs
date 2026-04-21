using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Calendar;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class CalendarSyncService : ICalendarSyncService
{
    private const int UpcomingLookbackDays = 7;      // show recently-passed sessions for context
    private const int UpcomingLookAheadDays = 180;   // 6-month window

    private readonly TimeZoneInfo _montrealTz;
    private readonly IUserRepository _userRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly ILogger<CalendarSyncService> _logger;

    public CalendarSyncService(
        IUserRepository userRepository,
        ISessionRegistrationRepository registrationRepository,
        ILogger<CalendarSyncService> logger)
    {
        _userRepository = userRepository;
        _registrationRepository = registrationRepository;
        _logger = logger;
        _montrealTz = ResolveMontrealTimeZone(logger);
    }

    public async Task<CalendarFeedDto> EnsureTokenAsync(Guid userId, string baseUrl)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        if (string.IsNullOrEmpty(user.CalendarFeedToken))
        {
            user.CalendarFeedToken = GenerateToken();
            await _userRepository.UpdateAsync(user);
        }

        return BuildDto(user.CalendarFeedToken!, baseUrl);
    }

    public async Task<CalendarFeedDto> RegenerateTokenAsync(Guid userId, string baseUrl)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        user.CalendarFeedToken = GenerateToken();
        await _userRepository.UpdateAsync(user);
        return BuildDto(user.CalendarFeedToken!, baseUrl);
    }

    public async Task<string?> BuildIcsForTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var user = await _userRepository.GetByCalendarFeedTokenAsync(token);
        if (user is null)
            return null;

        var now = DateTime.UtcNow;
        var rangeStart = now.AddDays(-UpcomingLookbackDays);
        var rangeEnd = now.AddDays(UpcomingLookAheadDays);

        var registrations = await _registrationRepository.GetByUserIdInRangeAsync(user.Id, rangeStart, rangeEnd);
        var events = registrations
            .Where(r => r.Session is not null)
            .Select(r => BuildEvent(r, user))
            .ToList();

        return BuildCalendar(user, events);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static CalendarFeedDto BuildDto(string token, string baseUrl)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var httpUrl = $"{trimmedBase}/api/v1/calendar/{token}.ics";
        var webcalUrl = httpUrl.Replace("https://", "webcal://").Replace("http://", "webcal://");
        return new CalendarFeedDto { Token = token, FeedUrl = httpUrl, WebcalUrl = webcalUrl };
    }

    private (DateTime startUtc, DateTime endUtc) ToUtcRange(Session session)
    {
        var startLocal = SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime);
        var endLocal = SessionTimeHelper.CombineLocal(session.SessionDate, session.EndTime);
        if (endLocal <= startLocal) endLocal = endLocal.AddHours(2); // defensive fallback
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, _montrealTz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, _montrealTz);
        return (startUtc, endUtc);
    }

    private string BuildEvent(SessionRegistration registration, ApplicationUser user)
    {
        var session = registration.Session!;
        var (startUtc, endUtc) = ToUtcRange(session);
        var uid = $"session-{session.Id}@sainthenribasketball.com";
        var lang = user.PreferredLanguage;
        var summary = EmailTemplateHelper.L("Saint-Henri Basketball session", "Séance Saint-Henri Basketball", lang);
        var location = session.Location ?? "Saint-Henri";
        var description = registration.PaymentPlan == PaymentPlan.Season
            ? EmailTemplateHelper.L("Season pass", "Forfait de saison", lang)
            : EmailTemplateHelper.L("Drop-in session", "Séance à la carte", lang);

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}");
        sb.AppendLine($"DTSTAMP:{FormatUtc(DateTime.UtcNow)}");
        sb.AppendLine($"DTSTART:{FormatUtc(startUtc)}");
        sb.AppendLine($"DTEND:{FormatUtc(endUtc)}");
        sb.AppendLine($"SUMMARY:{EscapeIcs(summary)}");
        sb.AppendLine($"LOCATION:{EscapeIcs(location)}");
        sb.AppendLine($"DESCRIPTION:{EscapeIcs(description)}");
        sb.Append("END:VEVENT");
        return sb.ToString();
    }

    private static string BuildCalendar(ApplicationUser user, IReadOnlyList<string> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Saint-Henri Basketball//Sessions Feed//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:SHB — {EscapeIcs(user.FirstName ?? "Sessions")}");
        sb.AppendLine("X-WR-TIMEZONE:America/Montreal");
        foreach (var evt in events)
            sb.AppendLine(evt);
        sb.Append("END:VCALENDAR");
        return sb.ToString();
    }

    private static string FormatUtc(DateTime utc) =>
        utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string EscapeIcs(string input) =>
        input.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private static TimeZoneInfo ResolveMontrealTimeZone(ILogger logger)
    {
        // Cross-platform: try IANA name first (Linux/macOS), fall back to Windows id.
        foreach (var id in new[] { "America/Montreal", "America/Toronto", "Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        logger.LogError("Could not resolve America/Montreal timezone; ICS feed will emit times as UTC, which will appear off by several hours to users.");
        return TimeZoneInfo.Utc;
    }
}
