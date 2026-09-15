using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.DTOs.Users;

/// Query string for the admin players list.
public class UserDirectoryQuery
{
    public string? Search { get; set; }
    public bool? IsAdmin { get; set; }
    public PaymentPlan? Plan { get; set; }
    public UserAccountFilter Account { get; set; } = UserAccountFilter.Active;
    /// High, Medium, Low or Inactive.
    public string? Engagement { get; set; }
    public UserSearchSort Sort { get; set; } = UserSearchSort.Name;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UserDirectoryItemDto
{
    public required UserDto User { get; set; }
    /// Percentage of the last 60 days' sessions attended.
    public double AttendanceRate { get; set; }
    public required string EngagementTier { get; set; }
}

public class UserDirectoryPageDto
{
    public IReadOnlyList<UserDirectoryItemDto> Items { get; set; } = Array.Empty<UserDirectoryItemDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    /// Admins among all matching players, not only this page.
    public int AdminCount { get; set; }
}
