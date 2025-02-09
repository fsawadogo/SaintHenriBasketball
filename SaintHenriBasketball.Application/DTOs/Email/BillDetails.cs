namespace SaintHenriBasketball.Application.DTOs.Email;

public class BillDetails
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string Location { get; set; } = "717 Saint-Ferdinand Street Montreal, QC H4C 3L7";
    public string PhoneNumber { get; set; } = "(438) 935-8129";
    public string PaymentMethod { get; set; } = "Interac e-Transfer";
    public string PaymentEmail { get; set; } = "pay@sainthenribasketball.com";
}