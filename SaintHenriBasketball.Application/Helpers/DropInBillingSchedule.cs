namespace SaintHenriBasketball.Application.Helpers;

/// When a session's drop-in players get billed. Pure: no database, no clock.
public static class DropInBillingSchedule
{
    /// Billing happens an hour after the session starts, so a Saturday 10:00 session is still billed at 11:00.
    public static readonly TimeSpan BillAfterStart = TimeSpan.FromHours(1);

    public static bool IsDue(DateTime sessionDate, string? startTime, DateTime nowUtc)
    {
        var startUtc = SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(sessionDate, startTime));
        return startUtc + BillAfterStart <= nowUtc;
    }
}
