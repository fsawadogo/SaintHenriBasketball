using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class EmailSendBudgetRepository : IEmailSendBudgetRepository
{
    private readonly ApplicationDbContext _context;

    public EmailSendBudgetRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// Failed attempts count too. A send that bounced still left the club's domain and still cost
    /// it standing with the receiving server, which is the thing the budget is protecting.
    public async Task<int> CountSentSinceAsync(EmailType type, DateTime since) =>
        await _context.EmailLogs.AsNoTracking()
            .CountAsync(e => e.EmailType == type && e.SentAt >= since);
}
