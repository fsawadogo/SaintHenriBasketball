using SaintHenriBasketball.Application.DTOs.Email;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// What one run of the season plan-choice email did, or would do.
public class PlanChoiceSendResultDto
{
    public Guid? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    public DateTime? StartDate { get; set; }
    public int DaysUntilStart { get; set; }
    /// True when nothing was sent, only counted.
    public bool DryRun { get; set; }
    public int Sent { get; set; }
    public int AlreadySent { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    /// Why the run did nothing at all, when it did nothing.
    public string? Outcome { get; set; }
    /// Who would receive it, on a dry run.
    public List<string> Recipients { get; set; } = new();
}

public interface ISeasonPlanChoiceEmailService
{
    /// Sends the plan-choice email for a season starting exactly <paramref name="daysAhead"/> days from
    /// today (Montreal). Safe to call repeatedly: a player who already has it is never sent it twice.
    Task<PlanChoiceSendResultDto> RunForSeasonStartingInAsync(int daysAhead, bool dryRun = false);

    /// The same email for one named season, whatever its start date. For an admin sending by hand.
    Task<PlanChoiceSendResultDto> RunForSeasonAsync(Guid seasonId, bool dryRun = false);

    /// The email as one player would receive it, for a preview.
    Task<string> PreviewAsync(Guid seasonId, Domain.Enums.EmailLanguage language);
}
