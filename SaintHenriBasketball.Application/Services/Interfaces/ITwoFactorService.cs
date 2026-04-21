using SaintHenriBasketball.Application.DTOs.TwoFactor;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ITwoFactorService
{
    Task<TwoFactorSetupDto> BeginSetupAsync(Guid userId);
    Task ConfirmSetupAsync(Guid userId, string code);
    Task<bool> VerifyCodeAsync(Guid userId, string code);
    Task DisableAsync(Guid userId, string code);
    Task<bool> IsTwoFactorRequiredAsync(Guid userId);
}
