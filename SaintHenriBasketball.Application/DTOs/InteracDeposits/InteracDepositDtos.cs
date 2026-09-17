using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.InteracDeposits;

/// One forwarded confirmation email, as the mail service sends it.
public class IngestInteracEmailDto
{
    /// The forwarding service's own id for the message, when it has one. Used to spot repeats.
    public string? MessageId { get; set; }
    public string? Subject { get; set; }
    /// Plain text or HTML; both are read.
    public string? Body { get; set; }
    /// When the bank sent it. Defaults to now.
    public DateTimeOffset? ReceivedAt { get; set; }
}

public class IngestInteracEmailResultDto
{
    /// parsed | duplicate | not-a-deposit
    public string Outcome { get; set; } = string.Empty;
    public Guid? DepositId { get; set; }
    public InteracDepositStatus? Status { get; set; }
    public InteracMatchConfidence? Confidence { get; set; }
    /// Set when the deposit paid off a payment on its own.
    public Guid? MatchedPaymentId { get; set; }
}

public class InteracDepositDto
{
    public Guid Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public decimal Amount { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public Guid? MatchedPaymentId { get; set; }
    public DateTime? MatchedOn { get; set; }
    public string? Note { get; set; }
    /// The player the matched payment belongs to, when there is one.
    public string? PlayerName { get; set; }
    /// Set for a likely match an admin has not confirmed: what the deposit would pay.
    public InteracSuggestionDto? Suggestion { get; set; }
}

public class InteracSuggestionDto
{
    public Guid PaymentId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
    /// Why this payment was suggested, in plain words.
    public string Reason { get; set; } = string.Empty;
}

public class MatchInteracDepositDto
{
    public Guid PaymentId { get; set; }
}

public class IgnoreInteracDepositDto
{
    public string? Note { get; set; }
}
