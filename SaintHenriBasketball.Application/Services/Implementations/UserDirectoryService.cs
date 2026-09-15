using AutoMapper;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class UserDirectoryService(IUserRepository users, ISessionRepository sessions, IMapper mapper) : IUserDirectoryService
{
    public const string UnknownEngagementMessage = "Choose an engagement level: High, Medium, Low or Inactive.";

    public async Task<UserDirectoryPageDto> SearchAsync(UserDirectoryQuery query)
    {
        var (page, pageSize) = ListPaging.Clamp(query.Page, query.PageSize, defaultPageSize: 20);
        var now = DateTime.UtcNow;
        var since = now.AddDays(-EngagementTiers.WindowDays);
        var sessionsInWindow = await sessions.CountSessionsBetweenAsync(since, now);

        int? minAttended = null, maxAttendedExclusive = null;
        if (!string.IsNullOrWhiteSpace(query.Engagement))
        {
            var tier = EngagementTiers.All.FirstOrDefault(t => t.Equals(query.Engagement.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ValidationException(UnknownEngagementMessage);
            var range = EngagementTiers.AttendedRange(tier, sessionsInWindow)!.Value;
            minAttended = range.Min;
            maxAttendedExclusive = range.MaxExclusive;
        }

        var result = await users.SearchAsync(new UserSearchCriteria(
            since,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.IsAdmin,
            query.Plan,
            query.Account,
            minAttended,
            maxAttendedExclusive,
            query.Sort,
            page,
            pageSize));

        return new UserDirectoryPageDto
        {
            Items = result.Items.Select(row =>
            {
                var rate = EngagementTiers.Rate(row.RecentAttended, sessionsInWindow);
                return new UserDirectoryItemDto
                {
                    User = mapper.Map<UserDto>(row.User),
                    AttendanceRate = Math.Round(rate, 1),
                    EngagementTier = EngagementTiers.Tier(rate),
                };
            }).ToList(),
            Total = result.Total,
            Page = page,
            PageSize = pageSize,
            AdminCount = result.AdminCount,
        };
    }
}
