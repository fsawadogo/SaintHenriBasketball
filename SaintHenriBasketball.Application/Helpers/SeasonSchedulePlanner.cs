using System.Text.RegularExpressions;
using SaintHenriBasketball.Application.DTOs.SeasonSchedule;
using SaintHenriBasketball.Application.Exceptions;

namespace SaintHenriBasketball.Application.Helpers;

/// One session the wizard would create.
public sealed record PlannedSession(DateTime Date, int DayOfWeek, string StartTime, string EndTime, int MaxCapacity, decimal DropInPrice, string Location);

/// Turns a season's dates and its session days into the sessions to create.
/// Pure: no database, no clock, so it can be checked on its own.
public static class SeasonSchedulePlanner
{
    public const int MaxSeasonLengthDays = 366;
    public const int MaxSessionDays = 14;
    public const int MaxSessions = 400;
    public const int MaxCapacityPerSession = 100;
    public const decimal MaxDropInPrice = 1000m;
    public const int MaxLocationLength = 300;
    public const int MaxNameLength = 100;
    public const int MaxNotesLength = 500;
    public const decimal MaxSeasonPrice = 10000m;

    private static readonly Regex TimePattern = new(@"^([01]?\d|2[0-3]):([0-5]\d)(:00)?$", RegexOptions.Compiled);

    /// "9:30", "09:30" and "09:30:00" all become "09:30". Anything else is null.
    public static string? NormalizeTime(string? value)
    {
        if (value is null) return null;
        var match = TimePattern.Match(value.Trim());
        return match.Success ? $"{int.Parse(match.Groups[1].Value):00}:{match.Groups[2].Value}" : null;
    }

    /// Checks the dates and the session days, and returns the days with their times normalised.
    /// Throws ValidationException naming the first problem.
    public static IReadOnlyList<SeasonScheduleDayDto> ValidateDays(DateTime startDate, DateTime endDate, IReadOnlyList<SeasonScheduleDayDto>? days)
    {
        if (endDate.Date < startDate.Date) throw new ValidationException("The end date must be on or after the start date.");
        if ((endDate.Date - startDate.Date).TotalDays + 1 > MaxSeasonLengthDays) throw new ValidationException($"A season can last at most {MaxSeasonLengthDays} days.");

        days ??= Array.Empty<SeasonScheduleDayDto>();
        if (days.Count > MaxSessionDays) throw new ValidationException($"A season can have at most {MaxSessionDays} session days.");

        var normalized = new List<SeasonScheduleDayDto>();
        var seen = new HashSet<(int, string)>();
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var label = $"Session day {index + 1}";
            if (day.DayOfWeek is < 0 or > 6) throw new ValidationException($"{label}: choose a day of the week.");

            var start = NormalizeTime(day.StartTime) ?? throw new ValidationException($"{label}: the start time must look like 10:00.");
            var end = NormalizeTime(day.EndTime) ?? throw new ValidationException($"{label}: the end time must look like 12:00.");
            if (string.CompareOrdinal(end, start) <= 0) throw new ValidationException($"{label}: the end time must be after the start time.");

            if (day.MaxCapacity is < 1 or > MaxCapacityPerSession) throw new ValidationException($"{label}: the number of spots must be between 1 and {MaxCapacityPerSession}.");
            if (day.DropInPrice < 0m || day.DropInPrice > MaxDropInPrice) throw new ValidationException($"{label}: the drop-in price must be between 0 and {MaxDropInPrice:0}.");

            var location = day.Location?.Trim();
            if (string.IsNullOrEmpty(location)) throw new ValidationException($"{label}: the gym address is required.");
            if (location.Length > MaxLocationLength) throw new ValidationException($"{label}: the gym address is at most {MaxLocationLength} characters.");

            if (!seen.Add((day.DayOfWeek, start))) throw new ValidationException($"{label}: this day and start time are already listed.");

            normalized.Add(new SeasonScheduleDayDto
            {
                DayOfWeek = day.DayOfWeek,
                StartTime = start,
                EndTime = end,
                MaxCapacity = day.MaxCapacity,
                DropInPrice = day.DropInPrice,
                Location = location,
            });
        }
        return normalized;
    }

    /// Checks the season's own details. Throws ValidationException naming the first problem.
    public static void ValidateSeason(string? name, decimal price, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("The season needs a name.");
        if (name.Trim().Length > MaxNameLength) throw new ValidationException($"The season name is at most {MaxNameLength} characters.");
        if (price < 0m || price > MaxSeasonPrice) throw new ValidationException($"The season price must be between 0 and {MaxSeasonPrice:0}.");
        if (notes is not null && notes.Length > MaxNotesLength) throw new ValidationException($"The notes are at most {MaxNotesLength} characters.");
    }

    /// Every session the days produce between the two dates, both included, in date then start-time order.
    /// Expects days that came back from ValidateDays.
    public static IReadOnlyList<PlannedSession> Plan(DateTime startDate, DateTime endDate, IReadOnlyList<SeasonScheduleDayDto> days)
    {
        var planned = new List<PlannedSession>();
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            foreach (var day in days.Where(d => d.DayOfWeek == (int)date.DayOfWeek))
                planned.Add(new PlannedSession(date, day.DayOfWeek, day.StartTime, day.EndTime, day.MaxCapacity, day.DropInPrice, day.Location));
        }
        if (planned.Count > MaxSessions) throw new ValidationException($"That would create {planned.Count} sessions; the limit is {MaxSessions}. Shorten the season or remove a day.");
        return planned.OrderBy(p => p.Date).ThenBy(p => p.StartTime, StringComparer.Ordinal).ToList();
    }

    /// How a session is identified when looking for duplicates: its date and its start time.
    public static (DateTime Date, string StartTime) Key(DateTime date, string? startTime) =>
        (date.Date, NormalizeTime(startTime) ?? startTime?.Trim() ?? string.Empty);
}
