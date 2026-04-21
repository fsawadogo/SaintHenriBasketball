namespace SaintHenriBasketball.Application.DTOs.TaxReceipts;

public class TaxReceiptYearDto
{
    public int Year { get; set; }
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class TaxReceiptLineDto
{
    public DateTime PaymentDate { get; set; }
    public string? Reference { get; set; }
    public string PlanLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TaxReceiptDto
{
    public int Year { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public decimal TotalAmount { get; set; }
    public List<TaxReceiptLineDto> Lines { get; set; } = new();
}
