using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

/// <summary>
/// One Interac auto-deposit confirmation email, as received.
///
/// The club's Interac transfers deposit automatically and the bank emails a confirmation. Until now
/// nothing read those emails: a player typed a reference number, an admin opened their banking app,
/// compared by hand and ticked a box. A deposit row is the bank's side of that comparison, kept so
/// the match can be checked later and so the same email is never counted twice.
/// </summary>
public class InteracDeposit
{
    public Guid Id { get; private set; }

    /// When the bank sent the confirmation (UTC). Falls back to when it reached us.
    public DateTime ReceivedAt { get; set; }

    public decimal Amount { get; set; }

    /// Who the bank says sent the money, as written in the email.
    public string SenderName { get; set; } = string.Empty;

    /// The bank's reference, when the email carries one.
    public string? ReferenceNumber { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// The email text, kept for an admin to read when a match is doubted. Trimmed to a few thousand characters.
    public string Body { get; set; } = string.Empty;

    /// The forwarding service's message id, when it sends one: the cheapest way to spot a repeat.
    public string? MessageId { get; set; }

    /// Fingerprint of the email's content, so the same deposit forwarded twice is stored once.
    public string Fingerprint { get; set; } = string.Empty;

    public InteracDepositStatus Status { get; set; }

    public InteracMatchConfidence Confidence { get; set; }

    public Guid? MatchedPaymentId { get; set; }
    public Payment? MatchedPayment { get; set; }

    /// When a payment was marked paid because of this deposit.
    public DateTime? MatchedOn { get; set; }

    /// Why an admin set it aside, when they did.
    public string? Note { get; set; }

    public DateTime CreatedOn { get; private set; }

    private InteracDeposit() { } // For EF Core

    public InteracDeposit(DateTime receivedAt, decimal amount, string senderName, string? referenceNumber, string subject, string body, string fingerprint, string? messageId)
    {
        Id = Guid.NewGuid();
        ReceivedAt = receivedAt;
        Amount = amount;
        SenderName = senderName;
        ReferenceNumber = referenceNumber;
        Subject = subject;
        Body = body;
        Fingerprint = fingerprint;
        MessageId = messageId;
        Status = InteracDepositStatus.Unmatched;
        Confidence = InteracMatchConfidence.None;
        CreatedOn = DateTime.UtcNow;
    }
}
