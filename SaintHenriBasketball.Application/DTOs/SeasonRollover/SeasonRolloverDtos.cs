using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.SeasonRollover;

/// Optional overrides for the proposed season. Anything left null is derived from the source season.
public class SeasonRolloverRequestDto
{
    public string? Name { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    /// The season fee.
    public decimal? Price { get; set; }
}

/// Body of the invite call. The route carries the new season; this names the season whose players are invited.
public class SeasonRenewalInviteRequestDto
{
    public Guid SourceSeasonId { get; set; }
    /// Send again even though invitations for the new season were already sent.
    public bool Resend { get; set; }
}

public class RolloverSeasonSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public SeasonStatus Status { get; set; }
}

public class RolloverProposedSeasonDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public int LengthDays { get; set; }
    /// Which of name, startDate, endDate and price came from the request.
    public List<string> OverriddenFields { get; set; } = new();
}

/// The slot every proposed session uses, copied from the source season's latest session.
public class RolloverSessionPlanDto
{
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public string Location { get; set; } = string.Empty;
    /// Null when the source season had no sessions and the defaults are used.
    public Guid? BasedOnSessionId { get; set; }
}

public class RolloverSessionDto
{
    /// Calendar date in Montreal, stored like every Session.SessionDate (midnight, no time zone).
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public int MaxCapacity { get; set; }
    public decimal DropInPrice { get; set; }
    public string Location { get; set; } = string.Empty;
    /// A session already exists on this date, so creating the draft skips it.
    public bool AlreadyExists { get; set; }
}

public class RolloverExistingSessionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Location { get; set; }
    public SessionStatus Status { get; set; }
}

public class RolloverConflictsDto
{
    /// A season with exactly these start and end dates exists; creating the draft is refused.
    public bool DuplicateSeason { get; set; }
    public List<RolloverSeasonSummaryDto> OverlappingSeasons { get; set; } = new();
    public List<RolloverExistingSessionDto> ExistingSessions { get; set; } = new();
}

public class SeasonRolloverPreviewDto
{
    public RolloverSeasonSummaryDto SourceSeason { get; set; } = new();
    public RolloverProposedSeasonDto ProposedSeason { get; set; } = new();
    public RolloverSessionPlanDto SessionPlan { get; set; } = new();
    public List<RolloverSessionDto> Sessions { get; set; } = new();
    public int SessionsToCreate { get; set; }
    public int SessionsToSkip { get; set; }
    public RolloverConflictsDto Conflicts { get; set; } = new();
    public bool CanCreate { get; set; }
}

public class SeasonRolloverDraftResultDto
{
    public Guid SeasonId { get; set; }
    public Guid SourceSeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }
    public SeasonStatus Status { get; set; }
    public int SessionsCreated { get; set; }
    public int SessionsSkipped { get; set; }
    public List<DateTime> CreatedDates { get; set; } = new();
    public List<DateTime> SkippedDates { get; set; } = new();
}

public class RenewalInviteSkipCountDto
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class RenewalInvitePlayerDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// Skip reason; null for a failed send.
    public string? Reason { get; set; }
}

public class SeasonRenewalInviteResultDto
{
    public Guid SeasonId { get; set; }
    public Guid SourceSeasonId { get; set; }
    /// "completedSeasonPayments" or "seasonPlanPlayers"; null when nothing was sent because invites already went out.
    public string? RecipientRule { get; set; }
    /// Invitations were already sent and resend was false, so nothing was sent this time.
    public bool AlreadySent { get; set; }
    public DateTime? PreviouslySentAt { get; set; }
    public int Candidates { get; set; }
    public int Sent { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<RenewalInviteSkipCountDto> SkippedReasons { get; set; } = new();
    public List<RenewalInvitePlayerDto> SkippedPlayers { get; set; } = new();
    public List<RenewalInvitePlayerDto> FailedPlayers { get; set; } = new();
}
