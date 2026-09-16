using SaintHenriBasketball.Application.DTOs.PublicSchedule;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Helpers;

/// Picks the sessions the public schedule page shows. Pure: no database, no clock.
public static class PublicScheduleSelector
{
    public static List<PublicSessionDto> Select(IEnumerable<Session> sessions, DateTime nowUtc, int take, bool includeFull)
    {
        return sessions
            .Where(s => s.Status == SessionStatus.Open || (includeFull && s.Status == SessionStatus.Full))
            // The repository compares dates only; hide sessions from earlier today that have ended.
            .Where(s => SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(s.SessionDate, s.EndTime, fallbackHour: 12)) > nowUtc)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime, StringComparer.Ordinal)
            .Take(take)
            .Select(s => new PublicSessionDto
            {
                Id = s.Id,
                SessionDate = s.SessionDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Location = s.Location,
                MaxCapacity = s.MaxCapacity,
                RegisteredPlayersCount = s.RegisteredPlayersCount,
                SpotsRemaining = Math.Max(0, s.MaxCapacity - s.RegisteredPlayersCount),
                DropInPrice = s.DropInPrice,
                IsFull = s.Status == SessionStatus.Full || s.RegisteredPlayersCount >= s.MaxCapacity,
            })
            .ToList();
    }
}
