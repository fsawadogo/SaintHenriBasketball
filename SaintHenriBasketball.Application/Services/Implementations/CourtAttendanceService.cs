using System.Globalization;
using SaintHenriBasketball.Application.DTOs.CourtAttendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class CourtAttendanceService(
    ICourtAttendanceRepository rosterRepository,
    IParticipationRepository participation,
    ISessionRepository sessionRepository,
    IPaymentRepository paymentRepository,
    ISeasonRepository seasonRepository,
    ICacheService cache) : ICourtAttendanceService
{
    public const int DefaultWindowDays = 90;
    /// Marking and walk-ins open when self check-in does: 30 minutes before the start. No closing time.
    public static readonly TimeSpan OpensBeforeStart = TimeSpan.FromMinutes(30);
    public const string OutcomeReason = "Court attendance";
    public const string WalkInReason = "Court attendance walk-in";

    private static readonly AttendanceOutcome[] SettableOutcomes = { AttendanceOutcome.Attended, AttendanceOutcome.NoShow, AttendanceOutcome.Unmarked };

    public async Task<SessionRosterDto> GetRosterAsync(Guid sessionId)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId) ?? throw new NotFoundException("Session not found");
        var entries = await rosterRepository.GetRosterAsync(sessionId);
        var seasons = entries.Any(e => e.PaymentPlan == PaymentPlan.Season) ? await SeasonsCoveringAsync(session) : Array.Empty<Season>();

        var players = new List<RosterPlayerDto>(entries.Count);
        foreach (var entry in entries) players.Add(await ToPlayerAsync(entry, session, seasons));

        return new SessionRosterDto
        {
            Session = new SessionRosterHeaderDto
            {
                SessionId = session.Id,
                SessionDate = session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                StartsAt = DateTime.SpecifyKind(StartUtc(session), DateTimeKind.Utc),
                EndsAt = DateTime.SpecifyKind(SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.EndTime, fallbackHour: 12)), DateTimeKind.Utc),
                Location = session.Location,
                Status = session.Status.ToString(),
                MaxCapacity = session.MaxCapacity,
                RegisteredPlayersCount = session.RegisteredPlayersCount,
                Counts = new RosterOutcomeCountsDto
                {
                    Total = entries.Count,
                    Unmarked = entries.Count(e => e.Outcome == AttendanceOutcome.Unmarked),
                    Attended = entries.Count(e => e.Outcome == AttendanceOutcome.Attended),
                    NoShow = entries.Count(e => e.Outcome == AttendanceOutcome.NoShow),
                    WalkIn = entries.Count(e => e.Outcome == AttendanceOutcome.WalkIn),
                },
            },
            Players = players,
        };
    }

    public async Task<RosterPlayerDto> SetOutcomeAsync(Guid sessionId, Guid userId, string? outcome)
    {
        var name = outcome?.Trim();
        var parsed = SettableOutcomes.Cast<AttendanceOutcome?>()
            .FirstOrDefault(o => string.Equals(o.ToString(), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("Outcome must be Attended, NoShow or Unmarked.");

        var session = await OpenSessionAsync(sessionId, "Attendance can be marked");
        await participation.SetOutcomeAsync(sessionId, userId, parsed, OutcomeReason);
        await SessionCacheKeys.InvalidateAsync(cache, sessionId, new[] { userId });
        return await PlayerAsync(session, userId);
    }

    public async Task<RosterPlayerDto> AddWalkInAsync(Guid sessionId, Guid userId)
    {
        if (userId == Guid.Empty) throw new ValidationException("Choose a player to add.");
        var session = await OpenSessionAsync(sessionId, "Walk-ins can be added");
        await participation.AddWalkInAsync(sessionId, userId, WalkInReason);
        await SessionCacheKeys.InvalidateAsync(cache, sessionId, new[] { userId });
        return await PlayerAsync(session, userId);
    }

    public async Task<NoShowStatsPageDto> GetNoShowStatsAsync(NoShowStatsQuery query)
    {
        var today = DateOnly.FromDateTime(SessionTimeHelper.ToLocal(DateTime.UtcNow));
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-DefaultWindowDays);
        if (from > to) throw new ValidationException("The start date must be on or before the end date.");
        var (page, pageSize) = ListPaging.Clamp(query.Page, query.PageSize);

        var now = DateTime.UtcNow;
        var rows = await rosterRepository.GetOutcomeRowsAsync(from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue), query.UserId);
        var stats = rows
            // Past sessions only: the session has ended.
            .Where(r => SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(r.SessionDate, r.EndTime, fallbackHour: 12)) <= now)
            .GroupBy(r => r.UserId)
            .Select(g =>
            {
                var first = g.First();
                var attended = g.Count(r => r.Outcome is AttendanceOutcome.Attended or AttendanceOutcome.WalkIn);
                var noShows = g.Count(r => r.Outcome == AttendanceOutcome.NoShow);
                var marked = attended + noShows;
                return new NoShowStatsItemDto
                {
                    UserId = first.UserId,
                    FirstName = first.FirstName,
                    LastName = first.LastName,
                    Name = FullName(first.FirstName, first.LastName),
                    Email = first.Email,
                    IsDeactivated = first.IsDeactivated,
                    SessionsRegistered = g.Count(),
                    Attended = attended,
                    NoShows = noShows,
                    Unmarked = g.Count(r => r.Outcome == AttendanceOutcome.Unmarked),
                    NoShowRate = marked > 0 ? Math.Round((double)noShows / marked * 100, 1) : 0,
                };
            })
            .OrderByDescending(s => s.NoShows)
            .ThenByDescending(s => s.NoShowRate)
            .ThenBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ThenBy(s => s.UserId)
            .ToList();

        return new NoShowStatsPageDto
        {
            Items = stats.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Total = stats.Count,
            Page = page,
            PageSize = pageSize,
            From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    private async Task<Session> OpenSessionAsync(Guid sessionId, string action)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId) ?? throw new NotFoundException("Session not found");
        if (session.Status == SessionStatus.Cancelled) throw new ValidationException("This session was cancelled.");
        if (DateTime.UtcNow < StartUtc(session) - OpensBeforeStart)
            throw new ValidationException($"{action} from 30 minutes before the session starts.");
        return session;
    }

    private async Task<RosterPlayerDto> PlayerAsync(Session session, Guid userId)
    {
        var entry = (await rosterRepository.GetRosterAsync(session.Id)).SingleOrDefault(e => e.UserId == userId)
            ?? throw new NotFoundException("This player is not on the session roster.");
        var seasons = entry.PaymentPlan == PaymentPlan.Season ? await SeasonsCoveringAsync(session) : Array.Empty<Season>();
        return await ToPlayerAsync(entry, session, seasons);
    }

    private async Task<RosterPlayerDto> ToPlayerAsync(CourtRosterEntry entry, Session session, IReadOnlyList<Season> seasonsCoveringSession)
    {
        var (paymentStatus, paymentId) = await PaymentStatusAsync(entry, session, seasonsCoveringSession);
        return new RosterPlayerDto
        {
            UserId = entry.UserId,
            FirstName = entry.FirstName,
            LastName = entry.LastName,
            Name = FullName(entry.FirstName, entry.LastName),
            Email = entry.Email,
            IsDeactivated = entry.IsDeactivated,
            PaymentPlan = entry.PaymentPlan.ToString(),
            IsAttending = entry.IsAttending,
            Outcome = entry.Outcome.ToString(),
            CheckInTime = entry.CheckInTime is DateTime checkIn ? DateTime.SpecifyKind(checkIn, DateTimeKind.Utc) : null,
            PaymentStatus = paymentStatus,
            PaymentId = paymentId,
        };
    }

    // Billing follows the player's current plan: drop-in players are billed per session (the payment the auto-billing
    // and checkout paths reuse), season players need a completed fee for a season whose dates include the session.
    private async Task<(string Status, Guid? PaymentId)> PaymentStatusAsync(CourtRosterEntry entry, Session session, IReadOnlyList<Season> seasonsCoveringSession)
    {
        if (entry.PaymentPlan == PaymentPlan.Season)
        {
            foreach (var season in seasonsCoveringSession)
            {
                var fee = await paymentRepository.GetByUserAndSeasonAsync(entry.UserId, season.Id);
                if (fee?.Status == PaymentStatus.Completed) return (RosterPaymentStatus.CoveredBySeasonPass, fee.Id);
            }
            return (RosterPaymentStatus.SeasonFeeUnpaid, null);
        }

        var dropIn = await paymentRepository.GetByUserAndSessionAsync(entry.UserId, session.Id);
        return dropIn?.Status switch
        {
            null => (RosterPaymentStatus.NotBilled, null),
            PaymentStatus.Completed => (RosterPaymentStatus.Completed, dropIn.Id),
            PaymentStatus.Failed => (RosterPaymentStatus.Failed, dropIn.Id),
            _ => (RosterPaymentStatus.Pending, dropIn.Id),
        };
    }

    private async Task<IReadOnlyList<Season>> SeasonsCoveringAsync(Session session)
    {
        var date = session.SessionDate.Date;
        return (await seasonRepository.GetAllAsync()).Where(s => s.StartDate.Date <= date && s.EndDate.Date >= date).ToList();
    }

    private static DateTime StartUtc(Session session) =>
        SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(session.SessionDate, session.StartTime));

    private static string FullName(string firstName, string lastName) => $"{firstName} {lastName}".Trim();
}
