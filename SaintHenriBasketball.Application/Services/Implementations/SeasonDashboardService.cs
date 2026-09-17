using SaintHenriBasketball.Application.DTOs.SeasonDashboard;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// <summary>
/// One season in one answer: passes sold against the cap, money collected and pending with its age,
/// and how each session filled. The same numbers exist today in four separate admin pages, none of
/// which is season-scoped, and passes-sold was never shown to an admin at all.
/// </summary>
public class SeasonDashboardService(ISeasonDashboardRepository repository) : ISeasonDashboardService
{
    /// Buckets for pending money, oldest last. Upper bound null means "and older".
    private static readonly (string Label, int From, int? To)[] AgeBuckets =
    {
        ("0-7 days", 0, 7),
        ("8-30 days", 8, 30),
        ("31-60 days", 31, 60),
        ("60+ days", 61, null),
    };

    public async Task<SeasonDashboardDto?> GetAsync(Guid? seasonId)
    {
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;

        Season? season;
        if (seasonId is Guid id)
        {
            season = await repository.GetSeasonAsync(id) ?? throw new NotFoundException("Season not found.");
        }
        else
        {
            season = await repository.GetCurrentOrNextSeasonAsync(today);
            if (season == null) return null;
        }

        // A season's last day counts in full, so compare against the day after it.
        var endExclusive = season.EndDate.Date.AddDays(1);
        var payments = await repository.GetSeasonPaymentsAsync(season.Id, season.StartDate.Date, endExclusive);
        var (paidPasses, unpaidChoices) = await repository.GetPassCountsAsync(season.Id);
        var rows = await repository.GetSessionRowsAsync(season.Id, season.StartDate.Date, endExclusive);
        var seasons = await repository.GetSeasonsAsync();

        var sessions = rows.Select(row => new SeasonDashboardSessionDto
        {
            SessionId = row.Session.Id,
            SessionDate = row.Session.SessionDate,
            StartTime = row.Session.StartTime,
            EndTime = row.Session.EndTime,
            Location = row.Session.Location,
            Status = row.Session.Status.ToString(),
            Capacity = row.Session.MaxCapacity,
            Reserved = row.Reserved,
            Attended = row.Attended,
            HasHappened = row.Session.SessionDate.Date < today,
            DropInsCollected = row.DropInsCollected,
            DropInsPending = row.DropInsPending,
        }).ToList();

        var played = sessions.Where(s => s.HasHappened && s.Status != nameof(SessionStatus.Cancelled)).ToList();
        var placesOffered = played.Sum(s => s.Capacity);

        return new SeasonDashboardDto
        {
            SeasonId = season.Id,
            Name = season.Name,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            Price = season.Price,
            Passes = new SeasonPassesDto
            {
                Capacity = season.SeasonPassCapacity,
                Sold = paidPasses,
                Left = Math.Max(0, season.SeasonPassCapacity - paidPasses),
                Unpaid = unpaidChoices,
            },
            Money = Money(payments, today),
            Attendance = new SeasonAttendanceDto
            {
                SessionsTotal = sessions.Count,
                SessionsPlayed = played.Count,
                ReservedTotal = sessions.Sum(s => s.Reserved),
                AttendedTotal = played.Sum(s => s.Attended),
                FillRate = placesOffered == 0 ? 0 : Math.Round((double)played.Sum(s => s.Attended) / placesOffered, 4),
            },
            Sessions = sessions,
            Seasons = seasons.Select(s => new SeasonOptionDto
            {
                SeasonId = s.Id,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
            }).ToList(),
        };
    }

    private static SeasonMoneyDto Money(IReadOnlyList<Payment> payments, DateTime todayLocal)
    {
        var completed = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
        var pending = payments.Where(p => p.Status == PaymentStatus.Pending).ToList();

        // A payment's age is counted from when it was created, in Montreal days, so "8 days pending"
        // means the same thing to the treasurer as it does on the balances page.
        int AgeInDays(Payment p) => Math.Max(0, (todayLocal - SessionTimeHelper.ToLocal(p.CreatedAt).Date).Days);

        var buckets = AgeBuckets.Select(bucket =>
        {
            var inBucket = pending.Where(p =>
            {
                var age = AgeInDays(p);
                return age >= bucket.From && (bucket.To == null || age <= bucket.To);
            }).ToList();
            return new AgingBucketDto
            {
                Label = bucket.Label,
                Count = inBucket.Count,
                Amount = Round(inBucket.Sum(p => p.Amount)),
            };
        }).ToList();

        return new SeasonMoneyDto
        {
            Collected = Round(completed.Sum(p => p.Amount)),
            CollectedFromPasses = Round(completed.Where(p => p.Plan == PaymentPlan.Season).Sum(p => p.Amount)),
            CollectedFromDropIns = Round(completed.Where(p => p.Plan == PaymentPlan.DropIn).Sum(p => p.Amount)),
            Pending = Round(pending.Sum(p => p.Amount)),
            PendingCount = pending.Count,
            OldestPendingDays = pending.Count == 0 ? null : pending.Max(AgeInDays),
            PendingByAge = buckets,
            Refunded = Round(payments.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.Amount)),
        };
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
