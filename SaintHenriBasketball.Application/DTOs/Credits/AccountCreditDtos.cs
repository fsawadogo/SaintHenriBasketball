using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.DTOs.Credits;

public class AccountCreditsDto
{
    public decimal Balance { get; set; }
    public IReadOnlyList<AccountCreditEntryDto> Entries { get; set; } = Array.Empty<AccountCreditEntryDto>();
}

public class AccountCreditEntryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public AccountCreditKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? PaymentId { get; set; }
}
