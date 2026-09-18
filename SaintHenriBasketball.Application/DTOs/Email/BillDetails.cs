namespace SaintHenriBasketball.Application.DTOs.Email;

public class BillDetails
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    /// What is actually charged, after any discount and credit. This is the figure on the receipt.
    public decimal Amount { get; set; }
    /// The price before a promo discount or account credit was applied, when either was.
    /// Read it as <c>OriginalAmount ?? Amount</c>, matching Payment.
    public decimal? OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CreditApplied { get; set; }
    /// When the money arrived, if it has. Null on a bill for something not yet paid.
    public DateTime? PaidOn { get; set; }
    public string? Reference { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string Location { get; set; } = "717 Saint-Ferdinand Street Montreal, QC H4C 3L7";
    public string PhoneNumber { get; set; } = "(438) 935-8129";
    public string PaymentMethod { get; set; } = "Interac e-Transfer";
    public string PaymentEmail { get; set; } = "pay@sainthenribasketball.com";
}