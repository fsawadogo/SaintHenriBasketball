using System.Globalization;

namespace SaintHenriBasketball.Application.Helpers;

/// When a session's drop-in players get billed. Pure: no database, no clock.
public static class DropInBillingSchedule
{
    /// Billing happens an hour after the session starts, so a Saturday 10:00 session is still billed at 11:00.
    public static readonly TimeSpan BillAfterStart = TimeSpan.FromHours(1);

    /// The formats `SessionTimeHelper.CombineLocal` reads a start time in: `H:mm`, `HH:mm`, `HH:mm:ss`.
    private static readonly string[] StartTimeFormats = { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" };

    /// The session's start time as a time of day, or null when it can't be used. `Session` only rejects a
    /// null start time, so `""` and legacy formats reach here; billing must not guess a time for those.
    public static TimeSpan? ParseStartTime(string? startTime) =>
        TimeSpan.TryParseExact(startTime ?? string.Empty, StartTimeFormats, CultureInfo.InvariantCulture, out var parsed)
        && parsed >= TimeSpan.Zero && parsed < TimeSpan.FromDays(1)
            ? parsed
            : null;

    public static bool IsDue(DateTime sessionDate, string? startTime, DateTime nowUtc)
    {
        // No usable start time, no billing: the fallback hour would bill an evening session in the morning.
        if (ParseStartTime(startTime) is null) return false;
        var startUtc = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(sessionDate, startTime));
        return startUtc + BillAfterStart <= nowUtc;
    }
}
