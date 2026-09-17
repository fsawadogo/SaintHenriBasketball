using SaintHenriBasketball.Application.DTOs.SignupFunnel;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// <summary>
/// Where new players stop: signed up, confirmed their email, reserved a session, paid, played.
/// The ingredients existed in four different tables and were never counted together, so nobody
/// could say how many people who registered actually made it onto the court.
/// </summary>
public class SignupFunnelService(ISignupFunnelRepository repository) : ISignupFunnelService
{
    public const int DefaultDays = 90;
    public const int MaxDays = 1095;

    public async Task<SignupFunnelDto> GetAsync(DateTimeOffset? from, DateTimeOffset? to)
    {
        var nowLocal = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var toLocal = to?.UtcDateTime is DateTime toUtc ? SessionTimeHelper.ToLocal(toUtc).Date : nowLocal;
        var fromLocal = from?.UtcDateTime is DateTime fromUtc ? SessionTimeHelper.ToLocal(fromUtc).Date : toLocal.AddDays(-DefaultDays);

        if (fromLocal > toLocal) throw new ValidationException("The start of the period must fall before its end.");
        if ((toLocal - fromLocal).TotalDays > MaxDays) throw new ValidationException($"The period cannot be longer than {MaxDays} days.");

        // Both ends count in full: the period runs to the end of the last day, Montreal time.
        var fromInstant = SessionTimeHelper.ToUtc(fromLocal);
        var toExclusive = SessionTimeHelper.ToUtc(toLocal.AddDays(1));

        var rows = await repository.GetRowsAsync(fromInstant, toExclusive);

        var registered = rows.Count;
        var confirmed = rows.Count(r => r.EmailConfirmed);
        var reserved = rows.Count(r => r.FirstReservationAt != null);
        var paid = rows.Count(r => r.FirstPaymentAt != null);
        var played = rows.Count(r => r.FirstAttendanceAt != null);

        var counts = new (string Key, int Count)[]
        {
            ("registered", registered),
            ("confirmed", confirmed),
            ("reserved", reserved),
            ("paid", paid),
            ("played", played),
        };

        var stages = counts.Select((stage, index) => new FunnelStageDto
        {
            Key = stage.Key,
            Count = stage.Count,
            ShareOfRegistered = registered == 0 ? 0 : Math.Round((double)stage.Count / registered, 4),
            // Stages are not strictly nested — a player can pay without confirming their email — so a
            // later stage can hold more people than the one before it. Report that as no drop, never
            // as a negative one.
            DroppedSincePrevious = index == 0 ? 0 : Math.Max(0, counts[index - 1].Count - stage.Count),
        }).ToList();

        var byMonth = rows
            .GroupBy(r => SessionTimeHelper.ToLocal(r.RegisteredAt).ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => new FunnelCohortDto
            {
                Label = g.Key,
                Registered = g.Count(),
                Confirmed = g.Count(r => r.EmailConfirmed),
                Reserved = g.Count(r => r.FirstReservationAt != null),
                Paid = g.Count(r => r.FirstPaymentAt != null),
                Played = g.Count(r => r.FirstAttendanceAt != null),
            })
            .ToList();

        return new SignupFunnelDto
        {
            FromUtc = fromInstant,
            ToUtc = toExclusive.AddSeconds(-1),
            Stages = stages,
            ByMonth = byMonth,
            Deactivated = rows.Count(r => r.IsDeactivated),
            MedianDaysToFirstPlay = MedianDaysToPlay(rows),
        };
    }

    private static double? MedianDaysToPlay(IReadOnlyList<SignupFunnelRow> rows)
    {
        var waits = rows
            .Where(r => r.FirstAttendanceAt != null)
            .Select(r => (r.FirstAttendanceAt!.Value.Date - SessionTimeHelper.ToLocal(r.RegisteredAt).Date).TotalDays)
            // A session that ran the same day, or a backdated import, counts as no wait rather than a negative one.
            .Select(days => Math.Max(0, days))
            .OrderBy(days => days)
            .ToList();
        if (waits.Count == 0) return null;
        var middle = waits.Count / 2;
        var median = waits.Count % 2 == 1 ? waits[middle] : (waits[middle - 1] + waits[middle]) / 2.0;
        return Math.Round(median, 1);
    }
}
