namespace SaintHenriBasketball.Application.DTOs.Session;

public class CancelSessionRequest
{
    /// Shown to players in the cancellation email.
    public string? Reason { get; set; }

    /// Refund drop-ins players already paid as account credit. Otherwise they're left for the admin to handle.
    public bool RefundPaidToCredit { get; set; } = true;
}

public class BulkCancelSessionsRequest
{
    public List<Guid> SessionIds { get; set; } = new();
    public string? Reason { get; set; }
    public bool RefundPaidToCredit { get; set; } = true;
}

public class SessionCancellationPreviewDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public int RegisteredPlayers { get; set; }
    public int PendingPayments { get; set; }
    public int PaidPayments { get; set; }
    public decimal PaidAmount { get; set; }
}

public class SessionCancellationResultDto
{
    public Guid SessionId { get; set; }
    public int PlayersNotified { get; set; }
    /// Players who were waiting for a place rather than holding one; they are told too.
    public int WaitingPlayersNotified { get; set; }
    public int PaymentsVoided { get; set; }
    public int PaymentsRefunded { get; set; }
    public decimal RefundedAmount { get; set; }
    /// Paid drop-ins left as they were (the admin chose not to refund, or the refund failed).
    public int PaidNotRefunded { get; set; }
}

public class SessionCancellationFailureDto
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BulkSessionCancellationResultDto
{
    public int Total { get; set; }
    public List<SessionCancellationResultDto> Results { get; set; } = new();
    public List<SessionCancellationFailureDto> Failed { get; set; } = new();
}
