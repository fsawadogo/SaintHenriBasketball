using SaintHenriBasketball.Application.DTOs.Reconciliation;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IReconciliationService
{
    Task<IReadOnlyList<PendingPaymentDto>> GetPendingAsync();
    Task<BulkCompletePaymentsResultDto> BulkCompleteAsync(IEnumerable<Guid> paymentIds);
}
