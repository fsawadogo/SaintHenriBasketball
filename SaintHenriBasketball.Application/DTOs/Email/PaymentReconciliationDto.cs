using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class PaymentReconciliationDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }
    public int CompletedPayments { get; set; }
    public int PendingPayments { get; set; }
    public int FailedPayments { get; set; }
    public Dictionary<PaymentPlan, int>? PaymentsByPlan { get; set; }
    public decimal CompletedAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal FailedAmount { get; set; }
}