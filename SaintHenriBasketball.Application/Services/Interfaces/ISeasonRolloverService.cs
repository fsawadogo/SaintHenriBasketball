using SaintHenriBasketball.Application.DTOs.SeasonRollover;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Starts the next season from a finished one and invites its players to renew.
public interface ISeasonRolloverService
{
    /// Proposes the next season, its Saturday sessions and any conflicts. Writes nothing.
    /// Throws NotFoundException for an unknown source season, ValidationException for invalid overrides.
    Task<SeasonRolloverPreviewDto> PreviewAsync(Guid sourceSeasonId, SeasonRolloverRequestDto? request);

    /// Creates the proposed season as Closed (not current) with its Saturday sessions.
    /// Throws ValidationException when a season with the same start and end dates exists.
    Task<SeasonRolloverDraftResultDto> CreateDraftAsync(Guid sourceSeasonId, SeasonRolloverRequestDto? request, Guid? adminId, string adminName);

    /// Emails the source season's players an invitation to renew for the new season, once unless resend is set.
    /// Throws ValidationException while the new season is Closed: open it (closing the current season) first.
    Task<SeasonRenewalInviteResultDto> SendRenewalInvitesAsync(Guid newSeasonId, SeasonRenewalInviteRequestDto request, Guid? adminId, string adminName);
}
