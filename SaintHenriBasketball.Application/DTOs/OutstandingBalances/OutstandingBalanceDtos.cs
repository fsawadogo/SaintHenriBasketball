namespace SaintHenriBasketball.Application.DTOs.OutstandingBalances;

public class OutstandingBalancesQuery
{
    /// SeasonFee, DropIn or Transfer (case-insensitive); empty for every kind.
    public string? Kind { get; set; }
    /// "age" (default) or "amount".
    public string? Sort { get; set; }
    /// "desc" (default: oldest or largest first) or "asc".
    public string? Direction { get; set; }
}

public class BalanceKindTotalDto
{
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

/// Totals for every outstanding debt, whatever kind filter the request used.
public class BalanceTotalsDto
{
    public BalanceKindTotalDto SeasonFee { get; set; } = new();
    public BalanceKindTotalDto DropIn { get; set; } = new();
    public BalanceKindTotalDto Transfer { get; set; } = new();
    public BalanceKindTotalDto All { get; set; } = new();
}

public class OutstandingBalanceRowDto
{
    /// Stable id of the debt, sent back to select it for reminders: "season:{seasonId}:{userId}", "dropin:{paymentId}" or "transfer:{paymentId}".
    public string Key { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Kind { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// When the money became due (UTC).
    public DateTime Since { get; set; }
    public int DaysOutstanding { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? SessionId { get; set; }
    /// Start of the drop-in session (UTC).
    public DateTime? SessionDate { get; set; }
    public Guid? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    /// The bank confirmation number the player submitted, for transfers.
    public string? InteracReference { get; set; }
    public DateTime? LastReminderSentAt { get; set; }
    /// False when the player turned off payment reminders or email notifications; a reminder would be skipped.
    public bool CanRemind { get; set; }
}

public class OutstandingBalancesDto
{
    public BalanceTotalsDto Totals { get; set; } = new();
    public IReadOnlyList<OutstandingBalanceRowDto> Items { get; set; } = Array.Empty<OutstandingBalanceRowDto>();
    public DateTime GeneratedAt { get; set; }
}

/// Either the selected row keys, or a kind to remind every row of that kind. With both, only selected rows of that kind.
public class SendRemindersRequestDto
{
    public List<string>? Keys { get; set; }
    public string? Kind { get; set; }
}

public class ReminderOutcomeDto
{
    public string Key { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? PlayerName { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SendRemindersResultDto
{
    /// Debts reminded. A player with several selected debts gets one email covering them.
    public int Sent { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int EmailsSent { get; set; }
    public List<ReminderOutcomeDto> SkippedItems { get; set; } = new();
    public List<ReminderOutcomeDto> FailedItems { get; set; } = new();
}

public class ReminderLogDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? SeasonId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime SentAt { get; set; }
    public Guid? SentByUserId { get; set; }
}

public class ReminderLogPageDto
{
    public IReadOnlyList<ReminderLogDto> Items { get; set; } = Array.Empty<ReminderLogDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
