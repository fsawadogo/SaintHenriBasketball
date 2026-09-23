namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IRegistrationChallengeService
{
    bool IsEnabled { get; }

    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default);
}
