using SaintHenriBasketball.Application.DTOs.Reconciliation;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IReconciliationService
{
    Task<IReadOnlyList<PendingPaymentDto>> GetPendingAsync();
    /// Completes only payments that are still pending Interac submissions; others are skipped.
    Task<BulkCompletePaymentsResultDto> BulkCompleteAsync(IEnumerable<Guid> paymentIds, Guid? adminId, string adminName);
}
