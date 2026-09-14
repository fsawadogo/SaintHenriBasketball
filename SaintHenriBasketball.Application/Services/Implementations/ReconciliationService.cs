using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Reconciliation;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class ReconciliationService : IReconciliationService
{
    private const int StaleThresholdDays = 7;
    // A player-submitted Interac transfer stores the bank reference after this marker.
    private const string InteracReferenceMarker = "|INTERAC:";

    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentService _paymentService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        IPaymentRepository paymentRepository,
        IPaymentService paymentService,
        IAuditLogRepository auditLogRepository,
        ILogger<ReconciliationService> logger)
    {
        _paymentRepository = paymentRepository;
        _paymentService = paymentService;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PendingPaymentDto>> GetPendingAsync()
    {
        var pending = await _paymentRepository.GetPaymentsByStatusAsync(PaymentStatus.Pending);
        var now = DateTime.UtcNow;

        return pending
            .Where(p => IsInteracSubmission(p.Reference))
            .Select(p =>
            {
                var days = Math.Max(0, (int)(now.Date - p.PaymentDate.Date).TotalDays);
                var name = p.User is null
                    ? "(unknown user)"
                    : $"{p.User.FirstName} {p.User.LastName}".Trim();

                return new PendingPaymentDto
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserName = string.IsNullOrWhiteSpace(name) ? (p.User?.Email ?? "(unknown)") : name,
                    UserEmail = p.User?.Email,
                    Amount = p.Amount,
                    Plan = p.Plan,
                    Reference = p.Reference,
                    PaymentDate = p.PaymentDate,
                    DaysPending = days,
                    IsStale = days >= StaleThresholdDays,
                };
            })
            .OrderByDescending(d => d.IsStale)
            .ThenByDescending(d => d.DaysPending)
            .ToList();
    }

    public async Task<BulkCompletePaymentsResultDto> BulkCompleteAsync(IEnumerable<Guid> paymentIds, Guid? adminId, string adminName)
    {
        var result = new BulkCompletePaymentsResultDto();
        foreach (var id in paymentIds.Distinct())
        {
            try
            {
                // Re-read each payment: the list the admin selected from may be stale.
                var payment = await _paymentRepository.GetByIdAsync(id);
                if (payment is null || payment.Status != PaymentStatus.Pending || !IsInteracSubmission(payment.Reference))
                {
                    result.Skipped++;
                    result.SkippedIds.Add(id);
                    continue;
                }

                await _paymentService.UpdatePaymentStatusAsync(id, PaymentStatus.Completed);
                result.Completed++;
                await WriteAuditAsync(payment, adminId, adminName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bulk reconciliation failed for payment {PaymentId}", id);
                result.Failed++;
                result.FailedIds.Add(id);
            }
        }
        return result;
    }

    private static bool IsInteracSubmission(string? reference) =>
        reference?.Contains(InteracReferenceMarker, StringComparison.Ordinal) == true;

    private async Task WriteAuditAsync(Payment payment, Guid? adminId, string adminName)
    {
        try
        {
            await _auditLogRepository.AddAsync(new AuditLog(
                action: "Payment.InteracReconciled",
                entityType: nameof(Payment),
                entityId: payment.Id,
                details: $"Amount: {payment.Amount:0.00}",
                userId: adminId,
                userName: adminName));
        }
        catch (Exception ex)
        {
            // The payment is already completed; a missing audit row must not report it as failed.
            _logger.LogWarning(ex, "Audit entry failed for reconciled payment {PaymentId}", payment.Id);
        }
    }
}
