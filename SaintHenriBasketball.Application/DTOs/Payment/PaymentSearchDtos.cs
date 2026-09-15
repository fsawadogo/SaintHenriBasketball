namespace SaintHenriBasketball.Application.DTOs.Payment;

/// Totals for every payment matching the filters. Only completed payments count as collected.
public class PaymentSearchSummaryDto
{
    public int CompletedCount { get; set; }
    public decimal Collected { get; set; }
    public decimal SeasonCollected { get; set; }
    public decimal DropInCollected { get; set; }
}

public class PaymentSearchResultDto
{
    public IReadOnlyList<PaymentDto> Items { get; set; } = Array.Empty<PaymentDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public PaymentSearchSummaryDto Summary { get; set; } = new();
}
