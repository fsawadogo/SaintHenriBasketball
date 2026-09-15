using SaintHenriBasketball.Application.DTOs.Reconciliation;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IReconciliationService
{
    Task<IReadOnlyList<PendingPaymentDto>> GetPendingAsync();
    /// Completes only payments that are still pending Interac submissions; others are skipped.
    Task<BulkCompletePaymentsResultDto> BulkCompleteAsync(IEnumerable<Guid> paymentIds, Guid? adminId, string adminName);
    /// Marks still-pending Interac submissions as not received (failed); others are skipped.
    Task<BulkMarkNotReceivedResultDto> BulkMarkNotReceivedAsync(IEnumerable<Guid> paymentIds, string? note, Guid? adminId, string adminName);
}
