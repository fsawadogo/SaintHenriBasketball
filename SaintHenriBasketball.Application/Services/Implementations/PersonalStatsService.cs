using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Stats;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PersonalStatsService : IPersonalStatsService
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionAttendanceRepository _attendanceRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<PersonalStatsService> _logger;

    public PersonalStatsService(
        IUserRepository userRepository,
        ISessionAttendanceRepository attendanceRepository,
        ISessionRegistrationRepository registrationRepository,
        IPaymentRepository paymentRepository,
        ILogger<PersonalStatsService> logger)
    {
        _userRepository = userRepository;
        _attendanceRepository = attendanceRepository;
        _registrationRepository = registrationRepository;
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<BadgesSummaryDto> GetBadgesSummaryAsync(Guid userId)
    {
        _ = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        var attendanceTask = _attendanceRepository.GetUserAttendanceHistoryAsync(userId);
        var registrationsTask = _registrationRepository.GetByUserIdAsync(userId);
        await Task.WhenAll(attendanceTask, registrationsTask);

        var attendedPast = attendanceTask.Result
            .Where(a => a.IsAttending && a.Session is not null && a.Session.SessionDate <= DateTime.UtcNow)
            .OrderBy(a => a.Session!.SessionDate)
            .ToList();
        var registeredPast = registrationsTask.Result
            .Where(r => r.Session is not null && r.Session.SessionDate <= DateTime.UtcNow)
            .ToList();

        var (current, longest) = ComputeStreaks(registeredPast, attendedPast);

        return new BadgesSummaryDto
        {
            CurrentStreak = current,
            LongestStreak = longest,
            TotalAttended = attendedPast.Count,
            Badges = BadgeCatalog.ComputeEarned(attendedPast.Count, longest, attendedPast),
        };
    }

    public async Task<PersonalStatsDto> GetForUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        var attendanceTask = _attendanceRepository.GetUserAttendanceHistoryAsync(userId);
        var registrationsTask = _registrationRepository.GetByUserIdAsync(userId);
        var paymentsTask = _paymentRepository.GetPaymentsByUserAsync(userId);
        await Task.WhenAll(attendanceTask, registrationsTask, paymentsTask);

        var attendedPast = attendanceTask.Result
            .Where(a => a.IsAttending && a.Session is not null && a.Session.SessionDate <= DateTime.UtcNow)
            .OrderBy(a => a.Session!.SessionDate)
            .ToList();
        var registeredPast = registrationsTask.Result
            .Where(r => r.Session is not null && r.Session.SessionDate <= DateTime.UtcNow)
            .ToList();

        var totalAttended = attendedPast.Count;
        var totalRegistered = registeredPast.Count;
        var rate = totalRegistered == 0 ? 0d : (double)totalAttended / totalRegistered;

        var totalSpent = paymentsTask.Result
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        var (currentStreak, longestStreak) = ComputeStreaks(registeredPast, attendedPast);

        var favoriteDay = attendedPast
            .GroupBy(a => a.Session!.SessionDate.DayOfWeek)
            .OrderByDescending(g => g.Count())
            .Select(g => (DayOfWeek?)g.Key)
            .FirstOrDefault();

        var favoriteStart = attendedPast
            .Where(a => !string.IsNullOrEmpty(a.Session!.StartTime))
            .GroupBy(a => a.Session!.StartTime)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var monthlyRegistered = registeredPast
            .GroupBy(r => new { r.Session!.SessionDate.Year, r.Session.SessionDate.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Count());

        var monthlyAttended = attendedPast
            .GroupBy(a => new { a.Session!.SessionDate.Year, a.Session.SessionDate.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Count());

        var monthlyKeys = monthlyRegistered.Keys.Union(monthlyAttended.Keys)
            .OrderBy(k => k.Year).ThenBy(k => k.Month);

        var monthlyBreakdown = monthlyKeys.Select(k => new MonthlyAttendanceDto
        {
            Year = k.Year,
            Month = k.Month,
            Attended = monthlyAttended.TryGetValue(k, out var a) ? a : 0,
            Registered = monthlyRegistered.TryGetValue(k, out var r) ? r : 0,
        }).ToList();

        return new PersonalStatsDto
        {
            TotalRegistered = totalRegistered,
            TotalAttended = totalAttended,
            AttendanceRate = rate,
            TotalSpent = totalSpent,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            FavoriteDayOfWeek = favoriteDay,
            FavoriteStartTime = favoriteStart,
            FirstSessionOn = attendedPast.FirstOrDefault()?.Session!.SessionDate,
            LastSessionOn = attendedPast.LastOrDefault()?.Session!.SessionDate,
            MonthlyBreakdown = monthlyBreakdown,
            Badges = BadgeCatalog.ComputeEarned(totalAttended, longestStreak, attendedPast),
            CurrentPlan = user.PaymentPlan,
        };
    }

    /// Returns (current, longest) using each past registration as a slot;
    /// a slot counts toward the streak only if the user actually attended.
    /// Current streak is the trailing run of attended-true slots ending at "today".
    private static (int current, int longest) ComputeStreaks(
        IReadOnlyList<SessionRegistration> registeredPast,
        IReadOnlyList<SessionAttendance> attendedPast)
    {
        if (registeredPast.Count == 0) return (0, 0);

        var attendedSessionIds = attendedPast.Select(a => a.SessionId).ToHashSet();
        var ordered = registeredPast
            .OrderBy(r => r.Session!.SessionDate)
            .Select(r => attendedSessionIds.Contains(r.SessionId))
            .ToList();

        int longest = 0, run = 0;
        foreach (var attended in ordered)
        {
            if (attended) { run++; if (run > longest) longest = run; }
            else run = 0;
        }
        return (run, longest);
    }
}
