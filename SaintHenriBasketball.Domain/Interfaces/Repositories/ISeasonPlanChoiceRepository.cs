using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISeasonPlanChoiceRepository
{
    /// The player's choice for this season, or null if they have not chosen yet.
    Task<SeasonPlanChoice?> GetAsync(Guid seasonId, Guid userId);

    /// Records or replaces the player's choice for this season. Idempotent.
    Task<SeasonPlanChoice> UpsertAsync(Guid seasonId, Guid userId, PaymentPlan plan);

    /// How many players hold a season pass for this season.
    ///
    /// Counts DISTINCT USERS with at least one Completed season payment — never payment rows.
    /// A player can legitimately have more than one row for a season (an abandoned Pending Interac
    /// attempt alongside a completed card payment), and counting rows would over-count spots and
    /// declare a season sold out while seats remain.
    Task<int> CountPaidPassesAsync(Guid seasonId);

    /// True when this player already has a Completed season payment for this season. Used to let a
    /// player who has paid keep their pass even when the season is otherwise sold out.
    Task<bool> HasPaidPassAsync(Guid seasonId, Guid userId);

    /// Clears the choices for this season that are NOT backed by a completed payment, and returns
    /// how many were cleared. Paid passes are never touched — that is the whole safety contract of
    /// the admin reset.
    Task<int> ClearUnpaidForSeasonAsync(Guid seasonId);

    /// The user ids whose unpaid choices a reset would clear, without changing anything.
    Task<List<Guid>> GetUnpaidChoiceUserIdsAsync(Guid seasonId);
}
