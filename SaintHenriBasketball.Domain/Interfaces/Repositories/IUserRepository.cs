using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<ApplicationUser> GetByIdAsync(Guid id);
    Task<List<ApplicationUser>> GetUsersByIdsAsync(List<Guid> userIds);
    Task<ApplicationUser> GetByEmailAsync(string? email);
    Task<ApplicationUser> GetByUsernameAsync(string username);
    Task<bool> EmailExistsAsync(string? email);

    /// Every address already registered at these domains, for comparing mailboxes rather than
    /// spellings. See EmailCanonicalizer.
    Task<IReadOnlyList<string>> GetEmailsAtDomainsAsync(IReadOnlyList<string> domains);
    Task<bool> UsernameExistsAsync(string? username);
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task AddAsync(ApplicationUser user);
    Task UpdateAsync(ApplicationUser user);
    Task DeleteAsync(ApplicationUser user);
    Task<ApplicationUser?> GetByCalendarFeedTokenAsync(string token);
    Task<int> CountActiveAdminsAsync();

    /// Confirmed, not deactivated accounts, newest first, without their registrations.
    Task<IReadOnlyList<ApplicationUser>> GetActiveConfirmedUsersAsync();

    /// One page of players matching <paramref name="criteria"/> with their recent attendance, plus totals for every match.
    Task<UserSearchPage> SearchAsync(UserSearchCriteria criteria);
}