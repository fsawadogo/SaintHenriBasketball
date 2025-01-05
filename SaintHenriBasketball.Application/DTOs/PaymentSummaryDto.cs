namespace SaintHenriBasketball.Application.DTOs;

public class PaymentSummaryDto
{
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }
    public int SeasonPayments { get; set; }
    public int DropInPayments { get; set; }
    public decimal SeasonRevenue { get; set; }
    public decimal DropInRevenue { get; set; }
}