namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISmsReminderService
{
    Task<int> SendDueRemindersAsync();
}
