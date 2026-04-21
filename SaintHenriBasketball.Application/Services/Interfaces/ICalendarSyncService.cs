using SaintHenriBasketball.Application.DTOs.Calendar;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ICalendarSyncService
{
    Task<CalendarFeedDto> EnsureTokenAsync(Guid userId, string baseUrl);
    Task<CalendarFeedDto> RegenerateTokenAsync(Guid userId, string baseUrl);
    Task<string?> BuildIcsForTokenAsync(string token);
}
