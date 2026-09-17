using SaintHenriBasketball.Application.DTOs.SignupFunnel;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISignupFunnelService
{
    /// Counts for players who signed up in the period. Both ends are Montreal days and count in full;
    /// with neither given, the last 90 days.
    Task<SignupFunnelDto> GetAsync(DateTimeOffset? from, DateTimeOffset? to);
}
