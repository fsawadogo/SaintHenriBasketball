using Microsoft.Extensions.Logging;
using OtpNet;
using SaintHenriBasketball.Application.DTOs.TwoFactor;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class TwoFactorService : ITwoFactorService
{
    private const string Issuer = "Saint-Henri Basketball";
    private const int WindowStep = 1; // accepts one code before and after the current window

    private readonly IUserRepository _userRepository;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(
        IUserRepository userRepository,
        IFeatureFlagService featureFlagService,
        ILogger<TwoFactorService> logger)
    {
        _userRepository = userRepository;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<TwoFactorSetupDto> BeginSetupAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32 = Base32Encoding.ToString(secretBytes);

        user.TwoFactorSecret = base32;
        user.TwoFactorEnabled = false;
        await _userRepository.UpdateAsync(user);

        var accountLabel = Uri.EscapeDataString(user.Email ?? user.Username ?? userId.ToString());
        var issuerEncoded = Uri.EscapeDataString(Issuer);
        var otpAuthUri = $"otpauth://totp/{issuerEncoded}:{accountLabel}?secret={base32}&issuer={issuerEncoded}&digits=6&period=30";

        return new TwoFactorSetupDto { Secret = base32, OtpAuthUri = otpAuthUri };
    }

    public async Task ConfirmSetupAsync(Guid userId, string code)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
            throw new ValidationException("Two-factor setup has not been started");

        if (!ValidateTotp(user.TwoFactorSecret, code))
            throw new ValidationException("Invalid verification code");

        user.TwoFactorEnabled = true;
        await _userRepository.UpdateAsync(user);
        _logger.LogInformation("2FA enabled for user {UserId}", userId);
    }

    public async Task<bool> VerifyCodeAsync(Guid userId, string code)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret) || !user.TwoFactorEnabled)
            return false;

        return ValidateTotp(user.TwoFactorSecret, code);
    }

    public async Task DisableAsync(Guid userId, string code)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            throw new ValidationException("Two-factor authentication is not enabled");

        if (!ValidateTotp(user.TwoFactorSecret, code))
            throw new ValidationException("Invalid verification code");

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await _userRepository.UpdateAsync(user);
        _logger.LogInformation("2FA disabled for user {UserId}", userId);
    }

    public async Task<bool> IsTwoFactorRequiredAsync(Guid userId)
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Admin2fa))
            return false;

        var user = await _userRepository.GetByIdAsync(userId);
        return user is { TwoFactorEnabled: true, IsAdmin: true };
    }

    private static bool ValidateTotp(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            var secretBytes = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: WindowStep, future: WindowStep));
        }
        catch
        {
            return false;
        }
    }
}
