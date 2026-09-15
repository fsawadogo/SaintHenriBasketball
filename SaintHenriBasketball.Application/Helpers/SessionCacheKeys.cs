using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Application.Helpers;

/// Cached reads showing a session's players, counts and attendees, or a player's bookings. Clear them whenever
/// a reservation, attendance answer or session status changes, so admin screens and the attendee list aren't stale.
public static class SessionCacheKeys
{
    public const string UpcomingSessions = "UpcomingSessions";
    public const string AvailableSessions = "AvailableSessions";

    public static IReadOnlyList<string> For(Guid sessionId, IEnumerable<Guid>? userIds = null)
    {
        var keys = new List<string>
        {
            $"Attendance:Session:{sessionId}",
            $"Attendance:Session:{sessionId}:Summary",
            $"Attendance:Session:{sessionId}:Attendees",
            $"Session_{sessionId}",
            UpcomingSessions,
            AvailableSessions,
        };
        if (userIds != null) keys.AddRange(userIds.Distinct().Select(userId => $"Attendance:User:{userId}"));
        return keys;
    }

    public static async Task InvalidateAsync(ICacheService cache, Guid sessionId, IEnumerable<Guid>? userIds = null)
    {
        foreach (var key in For(sessionId, userIds))
            await cache.RemoveAsync(key);
    }
}
