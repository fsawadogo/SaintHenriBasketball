using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Reconciliation;

public class PendingPaymentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public decimal Amount { get; set; }
    public PaymentPlan Plan { get; set; }
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
    /// The drop-in session the transfer pays for, when it isn't a season payment.
    public DateTime? SessionDate { get; set; }
    public int DaysPending { get; set; }
    public bool IsStale { get; set; }
}

public class BulkCompletePaymentsDto
{
    public List<Guid> PaymentIds { get; set; } = new();
}

/// Transfers the club couldn't find in the bank: they become failed, the player is emailed and any credit
/// they used is returned.
public class BulkMarkNotReceivedDto
{
    public List<Guid> PaymentIds { get; set; } = new();
    public string? Note { get; set; }
}

public class BulkMarkNotReceivedResultDto
{
    public int MarkedNotReceived { get; set; }
    /// Payments no longer pending, or not Interac submissions, when the request arrived.
    public int Skipped { get; set; }
    public List<Guid> SkippedIds { get; set; } = new();
    public int Errors { get; set; }
    public List<Guid> ErrorIds { get; set; } = new();
}

public class BulkCompletePaymentsResultDto
{
    public int Completed { get; set; }
    /// Payments no longer pending, or not Interac submissions, when the request arrived.
    public int Skipped { get; set; }
    public List<Guid> SkippedIds { get; set; } = new();
    public int Failed { get; set; }
    public List<Guid> FailedIds { get; set; } = new();
}
