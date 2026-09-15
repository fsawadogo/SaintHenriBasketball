using SaintHenriBasketball.Application.DTOs.OutstandingBalances;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IOutstandingBalancesService
{
    /// Unpaid season fees, unpaid drop-ins and Interac transfers awaiting review. Throws ValidationException for an unknown kind or sort.
    Task<OutstandingBalancesDto> GetBalancesAsync(OutstandingBalancesQuery query);

    /// Emails reminders for the selected debts and records each outcome. Throws ValidationException for an empty or invalid selection.
    Task<SendRemindersResultDto> SendRemindersAsync(SendRemindersRequestDto request, Guid? adminId, string adminName);

    /// Reminder history, newest first. Throws NotFoundException when <paramref name="userId"/> is not a player.
    Task<ReminderLogPageDto> GetReminderHistoryAsync(Guid? userId, int page, int pageSize);
}
