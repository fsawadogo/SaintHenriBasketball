using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISeasonPlanService
{
    /// The open season and this player's standing in it, or null when no season is open.
    Task<SeasonPlanStateDto?> GetCurrentAsync(Guid userId);

    /// Records the player's plan for the open season and updates their current plan.
    /// Throws ValidationException when the season pass is sold out and this player has not paid.
    Task<SeasonPlanStateDto> ChooseAsync(Guid userId, PaymentPlan plan);

    /// What a reset would clear, without changing anything.
    Task<SeasonPlanResetDto> PreviewResetAsync(Guid seasonId);

    /// Clears the season's unpaid choices and puts those players back on drop-in.
    /// Paid passes are never touched.
    Task<SeasonPlanResetDto> ResetAsync(Guid seasonId, Guid adminId, string adminName);
}
