using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Filters for the admin payments list. Page is 1-based; From and To are UTC instants compared with PaymentDate.
public record PaymentSearchCriteria(
    string? Search = null,
    PaymentStatus? Status = null,
    PaymentPlan? Plan = null,
    Guid? SeasonId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50);

/// Totals over every payment matching the filters, not only the returned page.
public record PaymentSearchTotals(int Count, int CompletedCount, decimal Collected, decimal SeasonCollected, decimal DropInCollected);

public record PaymentSearchPage(IReadOnlyList<Payment> Items, PaymentSearchTotals Totals);

public enum UserAccountFilter
{
    Active,
    Deactivated,
    All,
}

public enum UserSearchSort
{
    Name,
    Newest,
    Attendance,
}

/// Filters for the admin players list. Recent attendance counts sessions attended since RecentSince;
/// MinRecentAttended and MaxRecentAttendedExclusive narrow the list to one engagement tier.
public record UserSearchCriteria(
    DateTime RecentSince,
    string? Search = null,
    bool? IsAdmin = null,
    PaymentPlan? Plan = null,
    UserAccountFilter Account = UserAccountFilter.Active,
    int? MinRecentAttended = null,
    int? MaxRecentAttendedExclusive = null,
    UserSearchSort Sort = UserSearchSort.Name,
    int Page = 1,
    int PageSize = 20);

public record UserSearchRow(ApplicationUser User, int RecentAttended);

public record UserSearchPage(IReadOnlyList<UserSearchRow> Items, int Total, int AdminCount);
