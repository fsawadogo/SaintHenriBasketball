using SaintHenriBasketball.Application.DTOs.Auth;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterUserDto registerDto);
    Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
    Task<IEnumerable<UserDto>> GetAllUsersAsync(); // Added method to get all users

}

