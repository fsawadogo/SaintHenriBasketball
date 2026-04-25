using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Application.Helpers;

/// Shared helpers for parsing session `StartTime`/`EndTime` strings and converting
/// between Montreal local time (how sessions are stored) and UTC.
public static class SessionTimeHelper
{
    public static readonly TimeZoneInfo MontrealTimeZone = ResolveMontrealTz();
    private static TimeZoneInfo MontrealTz => MontrealTimeZone;

    /// Combines a session date with an `HH:mm` (or `h:mm`) time string, returning an
    /// `Unspecified` DateTime. Callers convert to UTC via <see cref="ToUtc"/>.
    public static DateTime CombineLocal(DateTime date, string? time, double fallbackHour = 10)
    {
        if (TimeSpan.TryParseExact(time ?? string.Empty, new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var ts)
            || TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out ts))
        {
            return DateTime.SpecifyKind(date.Date.Add(ts), DateTimeKind.Unspecified);
        }
        return DateTime.SpecifyKind(date.Date.AddHours(fallbackHour), DateTimeKind.Unspecified);
    }

    /// Converts a Montreal-local DateTime to UTC.
    public static DateTime ToUtc(DateTime localMontreal)
    {
        var unspecified = DateTime.SpecifyKind(localMontreal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, MontrealTz);
    }

    /// Converts a UTC DateTime to Montreal-local time.
    public static DateTime ToLocal(DateTime utc)
    {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, MontrealTz);
    }

    /// Parses a session time string and returns the formatted `h:mm` for display.
    /// Falls back to the original string when parsing fails.
    public static string FormatDisplay(string? time)
    {
        if (TimeSpan.TryParseExact(time ?? string.Empty, new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var ts)
            || TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out ts))
        {
            return ts.ToString(@"h\:mm", CultureInfo.InvariantCulture);
        }
        return time ?? string.Empty;
    }

    private static TimeZoneInfo ResolveMontrealTz()
    {
        foreach (var id in new[] { "America/Montreal", "America/Toronto", "Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
