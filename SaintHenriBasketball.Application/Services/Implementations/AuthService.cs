using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using AutoMapper;
using SaintHenriBasketball.Application.DTOs.Auth;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _sessionRegistrationRepository;

    public AuthService(
        IConfiguration configuration,
        IMapper mapper,
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository sessionRegistrationRepository)
    {
        _configuration = configuration;
        _mapper = mapper;
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
        _sessionRegistrationRepository = sessionRegistrationRepository;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterUserDto registerDto)
    {
        if (await _userRepository.EmailExistsAsync(registerDto.Email))
        {
            throw new ValidationException("Email is already registered");
        }

        if (await _userRepository.UsernameExistsAsync(registerDto.Username))
        {
            throw new ValidationException("Username is already taken");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

        var user = new ApplicationUser(
            registerDto.Username,
            registerDto.Email,
            passwordHash,
            registerDto.FirstName,
            registerDto.LastName,
            registerDto.PaymentPlan
        );

        await _userRepository.AddAsync(user);

        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user),
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin,
            PaymentPlan = user.PaymentPlan
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByEmailAsync(loginDto.Email);

        if (user == null)
        {
            throw new ValidationException("Invalid credentials");
        }

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid credentials");
        }

        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user),
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin,
            PaymentPlan = user.PaymentPlan
        };
    }

    public async Task<UserDto> GetUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto updateDto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        if (!string.IsNullOrEmpty(updateDto.Email) && updateDto.Email != user.Email)
        {
            if (await _userRepository.EmailExistsAsync(updateDto.Email))
            {
                throw new ValidationException("Email is already taken");
            }
            user.Email = updateDto.Email;
        }

        if (!string.IsNullOrEmpty(updateDto.Username) && updateDto.Username != user.Username)
        {
            if (await _userRepository.UsernameExistsAsync(updateDto.Username))
            {
                throw new ValidationException("Username is already taken");
            }
            user.Username = updateDto.Username;
        }

        user.FirstName = updateDto.FirstName ?? user.FirstName;
        user.LastName = updateDto.LastName ?? user.LastName;
        user.PaymentPlan = updateDto.PaymentPlan;

        await _userRepository.UpdateAsync(user);
        return _mapper.Map<UserDto>(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllUsersAsync();
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<SessionRegistrationResponseDto> RegisterForSessionAsync(Guid userId, SessionRegistrationDto registrationDto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        var session = await _sessionRepository.GetByIdAsync(registrationDto.SessionId);
        if (session == null)
        {
            throw new NotFoundException("Session not found");
        }

        if (session.Status != SessionStatus.Open)
        {
            throw new ValidationException("Session is not open for registration");
        }

        if (session.RegisteredPlayersCount >= session.MaxCapacity)
        {
            throw new ValidationException("Session is at maximum capacity");
        }

        if (await _sessionRegistrationRepository.ExistsAsync(userId, registrationDto.SessionId))
        {
            throw new ValidationException("User is already registered for this session");
        }

        var registration = new SessionRegistration(userId, registrationDto.SessionId, user.PaymentPlan);
        
        await _sessionRegistrationRepository.AddAsync(registration);

        session.RegisteredPlayersCount++;
        await _sessionRepository.UpdateAsync(session);

        return _mapper.Map<SessionRegistrationResponseDto>(registration);
    }

    public async Task<IEnumerable<SessionDto>> GetUserSessionsAsync(Guid userId)
    {
        var sessions = await _sessionRepository.GetUserSessionsAsync(userId);
        return _mapper.Map<IEnumerable<SessionDto>>(sessions);
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        // Existing implementation remains the same
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:DurationInDays"])),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public async Task CancelSessionRegistrationAsync(Guid userId, Guid sessionId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
        {
            throw new NotFoundException("Session not found");
        }

        if (!await _sessionRegistrationRepository.ExistsAsync(userId, sessionId))
        {
            throw new NotFoundException("Registration not found");
        }

        await _sessionRegistrationRepository.DeleteAsync(userId, sessionId);

        // Update session's registered players count
        session.RegisteredPlayersCount--;
        await _sessionRepository.UpdateAsync(session);
    }
}