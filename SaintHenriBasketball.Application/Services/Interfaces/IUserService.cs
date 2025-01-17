using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> RegisterAsync(RegisterUserDto registerDto);
    Task<UserResponseDto> LoginAsync(LoginDto loginDto);
    Task<UserDto> GetUserAsync(Guid userId);
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto updateDto);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task DeleteUserAsync(Guid userId);
    Task ConfirmEmailAsync(string email, string token);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
    Task UpdateUserPaymentPlanAsync(Guid userId, PaymentPlan paymentPlan);
    Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null);
}

