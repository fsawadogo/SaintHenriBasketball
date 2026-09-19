using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// How much of one kind of email the club has already sent. The send log is the counter: it is
/// written on every send, survives a restart, and is shared by every instance — none of which is
/// true of a number held in memory, and an attacker who can rotate IPs can outlast any of those.
public interface IEmailSendBudgetRepository
{
    Task<int> CountSentSinceAsync(EmailType type, DateTime since);
}
