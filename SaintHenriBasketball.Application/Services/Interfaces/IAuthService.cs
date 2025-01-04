using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Auth;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterUserDto registerDto);
    Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
    Task<UserDto> GetUserAsync(Guid userId);
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto updateDto);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<SessionRegistrationResponseDto> RegisterForSessionAsync(Guid userId, SessionRegistrationDto registrationDto);
    Task<IEnumerable<SessionDto>> GetUserSessionsAsync(Guid userId);
    Task CancelSessionRegistrationAsync(Guid userId, Guid sessionId);

}

