using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(ApplicationDbContext context, ILogger<UserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetActiveConfirmedUsersAsync() =>
        await _context.Users.AsNoTracking()
            .Where(u => u.EmailConfirmed && !u.IsDeactivated)
            .OrderByDescending(u => u.CreatedOn)
            .ThenBy(u => u.Id)
            .ToListAsync();

    public Task<int> CountActiveAdminsAsync() =>
        _context.Users.CountAsync(u => u.IsAdmin && !u.IsDeactivated);

    public async Task<ApplicationUser> GetByIdAsync(Guid id)
    {
        return (await _context.Users
            .Include(u => u.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Id == id))!;
    }

    public async Task<ApplicationUser> GetByEmailAsync(string? email)
    {
        return (await _context.Users
            .Include(u => u.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Email != null && email != null && u.Email.ToLower() == email.ToLower()))!;
    }

    public async Task<ApplicationUser> GetByUsernameAsync(string username)
    {
        return (await _context.Users
            .Include(u => u.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == username.ToLower()))!;
    }

    public async Task<bool> EmailExistsAsync(string? email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email != null && email != null && u.Email.ToLower() == email.ToLower());
    }

    /// Every address already registered at these domains, so the caller can compare the mailboxes
    /// they reach rather than the way they are spelled. Narrowed by domain because that part is
    /// not rewritten: only the local side of a Gmail address is noise.
    public async Task<IReadOnlyList<string>> GetEmailsAtDomainsAsync(IReadOnlyList<string> domains)
    {
        if (domains.Count == 0) return Array.Empty<string>();

        var suffixes = domains.Select(d => "@" + d.ToLowerInvariant()).ToList();

        return await _context.Users.AsNoTracking()
            .Where(u => u.Email != null && suffixes.Any(s => u.Email.ToLower().EndsWith(s)))
            .Select(u => u.Email!)
            .ToListAsync();
    }

    public async Task<bool> UsernameExistsAsync(string? username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username != null && username != null && u.Username.ToLower() == username.ToLower());
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _context.Users
            .Include(u => u.SessionRegistrations)
                .ThenInclude(sr => sr.Session)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<UserSearchPage> SearchAsync(UserSearchCriteria criteria)
    {
        var users = _context.Users.AsNoTracking();
        users = criteria.Account switch
        {
            UserAccountFilter.Active => users.Where(u => !u.IsDeactivated),
            UserAccountFilter.Deactivated => users.Where(u => u.IsDeactivated),
            _ => users,
        };
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search.Trim();
            users = users.Where(u => (u.Email != null && u.Email.Contains(term))
                || (u.Username != null && u.Username.Contains(term))
                || ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Contains(term));
        }
        if (criteria.IsAdmin is bool isAdmin) users = users.Where(u => u.IsAdmin == isAdmin);
        if (criteria.Plan is { } plan) users = users.Where(u => u.PaymentPlan == plan);

        var since = criteria.RecentSince;
        var rows = users.Select(u => new
        {
            User = u,
            Recent = _context.SessionAttendances.Count(a => a.UserId == u.Id && a.IsAttending && a.CreatedOn >= since),
        });
        if (criteria.MinRecentAttended is int min) rows = rows.Where(r => r.Recent >= min);
        if (criteria.MaxRecentAttendedExclusive is int maxExclusive) rows = rows.Where(r => r.Recent < maxExclusive);

        var total = await rows.CountAsync();
        var adminCount = await rows.CountAsync(r => r.User.IsAdmin);
        var ordered = criteria.Sort switch
        {
            UserSearchSort.Newest => rows.OrderByDescending(r => r.User.CreatedOn).ThenBy(r => r.User.Id),
            UserSearchSort.Attendance => rows.OrderByDescending(r => r.Recent).ThenBy(r => r.User.FirstName).ThenBy(r => r.User.LastName).ThenBy(r => r.User.Id),
            _ => rows.OrderBy(r => r.User.FirstName).ThenBy(r => r.User.LastName).ThenBy(r => r.User.Id),
        };
        var page = await ordered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();

        return new UserSearchPage(page.Select(r => new UserSearchRow(r.User, r.Recent)).ToList(), total, adminCount);
    }

    public async Task AddAsync(ApplicationUser user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ApplicationUser user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(ApplicationUser user)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    public async Task<ApplicationUser?> GetByCalendarFeedTokenAsync(string token)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.CalendarFeedToken == token);
    }

    public async Task<List<ApplicationUser>> GetUsersByIdsAsync(List<Guid> userIds)
    {
        try
        {
            if (!userIds.Any())
            {
                return new List<ApplicationUser>();
            }

            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} users from {Total} requested IDs",
                users.Count, userIds.Count);

            if (users.Count < userIds.Count)
            {
                var missingIds = userIds.Except(users.Select(u => u.Id));
                _logger.LogWarning("Some requested user IDs were not found: {MissingIds}",
                    string.Join(", ", missingIds));
            }

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users by IDs. Requested IDs: {UserIds}",
                string.Join(", ", userIds));
            throw;
        }
    }
}