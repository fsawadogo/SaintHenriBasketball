using SaintHenriBasketball.Application.DTOs.Users;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// The admin players list: search, filters, engagement and paging run in the database.
public interface IUserDirectoryService
{
    Task<UserDirectoryPageDto> SearchAsync(UserDirectoryQuery query);
}
