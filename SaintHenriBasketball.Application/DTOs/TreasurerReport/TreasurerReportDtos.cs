namespace SaintHenriBasketball.Application.DTOs.TreasurerReport;

/// <summary>
/// A date range (From and To, UTC instants compared inclusively) or one season. Give one or the other.
/// </summary>
public class TreasurerReportQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? SeasonId { get; set; }
}

public record TreasurerReportExport(byte[] Content, string FileName, int RowCount);

public class TreasurerReportDto
{
    public TreasurerReportScopeDto Scope { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public TreasurerTotalsDto Overall { get; set; } = new();
    /// Calendar months in America/Toronto time, oldest first. Months with no activity inside the range are included.
    public List<TreasurerMonthDto> ByMonth { get; set; } = new();
    /// Seasons by start date, then "No season" (drop-ins and payments not tied to a season).
    public List<TreasurerSeasonDto> BySeason { get; set; } = new();
    /// Drop-in takings per session, oldest first. Only filled when the report is scoped to one
    /// season; empty for a date range, where "which sessions" has no answer.
    public List<TreasurerDropInSessionDto> DropInBySession { get; set; } = new();
}

/// <summary>What one session took in drop-in fees.</summary>
public class TreasurerDropInSessionDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public string? StartTime { get; set; }
    public string? Location { get; set; }
    /// Money the club kept for this session: completed payments, and ones refunded as account credit.
    public decimal Collected { get; set; }
    /// Distinct players who paid.
    public int PlayersPaid { get; set; }
}

public class TreasurerReportScopeDto
{
    public const string RangeKind = "range";
    public const string SeasonKind = "season";

    /// "range" or "season".
    public string Kind { get; set; } = RangeKind;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? SeasonId { get; set; }
    public string? SeasonName { get; set; }
    public string TimeZone { get; set; } = "America/Toronto";
}

public class MoneyCountDto
{
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class TreasurerMethodSplitDto
{
    public MoneyCountDto Interac { get; set; } = new();
    public MoneyCountDto Card { get; set; } = new();
    /// Admin-created or admin-completed payments, and payments fully covered by a promo code or credit.
    public MoneyCountDto Other { get; set; } = new();
}

public class TreasurerPlanSplitDto
{
    public MoneyCountDto DropIn { get; set; } = new();
    public MoneyCountDto Season { get; set; } = new();
}

public class TreasurerRefundsDto
{
    public MoneyCountDto Total { get; set; } = new();
    public MoneyCountDto Card { get; set; } = new();
    /// Given back as account credit. The club kept the money, so this doesn't reduce net collected.
    public MoneyCountDto AccountCredit { get; set; } = new();
    public MoneyCountDto Manual { get; set; } = new();
    /// Marked refunded without the refund flow, so no method was recorded. Treated as money returned.
    public MoneyCountDto Unrecorded { get; set; } = new();
    /// Card + Manual + Unrecorded: money that left the club.
    public MoneyCountDto MoneyReturned { get; set; } = new();
}

public class TreasurerTotalsDto
{
    /// Payments currently Completed, dated by PaymentDate (when the money arrived).
    public MoneyCountDto Collected { get; set; } = new();
    public TreasurerMethodSplitDto CollectedByMethod { get; set; } = new();
    public TreasurerPlanSplitDto CollectedByPlan { get; set; } = new();

    /// Payments currently Pending, dated by PaymentDate (when the payment was created).
    public MoneyCountDto Outstanding { get; set; } = new();
    public TreasurerMethodSplitDto OutstandingByMethod { get; set; } = new();

    /// DiscountAmount on Completed payments; Count is how many had a discount.
    public MoneyCountDto PromoCost { get; set; } = new();
    /// CreditApplied on Completed payments; Count is how many used credit. Credit on pending payments is only
    /// reserved, and failed or refunded payments give their credit back.
    public MoneyCountDto CreditApplied { get; set; } = new();

    /// Every payment whose money came in: Completed plus later Refunded, dated by PaymentDate.
    public MoneyCountDto Received { get; set; } = new();
    /// Refunded payments, dated by RefundedOn.
    public TreasurerRefundsDto Refunds { get; set; } = new();
    /// Received minus Refunds.MoneyReturned. Account-credit refunds aren't subtracted: the money stays with the club.
    public decimal NetCollected { get; set; }
}

public class TreasurerMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    /// "yyyy-MM".
    public string Label { get; set; } = "";
    /// Midnight on the 1st in Toronto, as a UTC instant.
    public DateTime StartUtc { get; set; }
    /// Start of the next month, exclusive.
    public DateTime EndUtc { get; set; }
    public TreasurerTotalsDto Totals { get; set; } = new();
}

public class TreasurerSeasonDto
{
    /// Null for payments not tied to a season.
    public Guid? SeasonId { get; set; }
    public string SeasonName { get; set; } = "";
    public TreasurerTotalsDto Totals { get; set; } = new();
}
