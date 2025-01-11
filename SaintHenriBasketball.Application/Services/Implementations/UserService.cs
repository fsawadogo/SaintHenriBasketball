using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class UserService : IUserService
{
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IConfiguration configuration,
        IMapper mapper,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<UserService> logger)
    {
        _configuration = configuration;
        _mapper = mapper;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<UserResponseDto> RegisterAsync(RegisterUserDto registerDto)
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

        // Generate and set email confirmation token
        user.EmailConfirmationToken = Guid.NewGuid().ToString("N");

        await _userRepository.AddAsync(user);

        try
        {
            // Send confirmation email
            var confirmationLink = $"{_configuration["AppUrl"]}/confirm-email?token={user.EmailConfirmationToken}&email={user.Email}";
            await _emailService.SendConfirmationEmailAsync(user.Email, confirmationLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", user.Email);
            // Continue with registration even if email fails
        }

        return new UserResponseDto
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

    public async Task<UserResponseDto> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByUsernameAsync(loginDto.UserName);

        if (user == null)
        {
            throw new ValidationException("Invalid credentials");
        }

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid credentials");
        }

        if (!user.EmailConfirmed)
        {
            throw new ValidationException("Please confirm your email before logging in");
        }

        return new UserResponseDto
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

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        await _userRepository.DeleteAsync(user);
    }

    public async Task ConfirmEmailAsync(string email, string token)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            throw new ValidationException("Invalid email");
        }

        if (user.EmailConfirmationToken != token)
        {
            throw new ValidationException("Invalid confirmation token");
        }

        if (user.EmailConfirmed)
        {
            throw new ValidationException("Email already confirmed");
        }

        user.EmailConfirmed = true;
        user.EmailConfirmationToken = null;
        
        await _userRepository.UpdateAsync(user);
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal user existence
            return;
        }

        user.PasswordResetToken = Guid.NewGuid().ToString("N");
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _userRepository.UpdateAsync(user);

        var resetLink = $"{_configuration["AppUrl"]}/reset-password?token={user.PasswordResetToken}&email={user.Email}";
        await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var user = await _userRepository.GetByEmailAsync(resetPasswordDto.Email);
        if (user == null)
        {
            throw new ValidationException("Invalid email");
        }

        if (user.PasswordResetToken != resetPasswordDto.Token)
        {
            throw new ValidationException("Invalid reset token");
        }

        if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            throw new ValidationException("Reset token has expired");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        await _userRepository.UpdateAsync(user);
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
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
}