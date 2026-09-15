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
                    SessionDate = p.Session?.SessionDate,
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
                await WriteAuditAsync(payment, adminId, adminName, "Payment.InteracReconciled", $"Amount: {payment.Amount:0.00}");
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

    public const int MaxNoteLength = 300;

    public async Task<BulkMarkNotReceivedResultDto> BulkMarkNotReceivedAsync(IEnumerable<Guid> paymentIds, string? note, Guid? adminId, string adminName)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > MaxNoteLength)
            throw new Exceptions.ValidationException($"Keep the note under {MaxNoteLength} characters.");

        var result = new BulkMarkNotReceivedResultDto();
        foreach (var id in paymentIds.Distinct())
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(id);
                if (payment is null || payment.Status != PaymentStatus.Pending || !IsInteracSubmission(payment.Reference))
                {
                    result.Skipped++;
                    result.SkippedIds.Add(id);
                    continue;
                }

                // Failed releases any credit the payment used and emails the player that the payment didn't go through.
                await _paymentService.UpdatePaymentStatusAsync(id, PaymentStatus.Failed);
                result.MarkedNotReceived++;
                await WriteAuditAsync(payment, adminId, adminName, "Payment.InteracNotReceived",
                    $"Amount: {payment.Amount:0.00}" + (note == null ? "" : $"; Note: {note}"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Marking payment {PaymentId} as not received failed", id);
                result.Errors++;
                result.ErrorIds.Add(id);
            }
        }
        return result;
    }

    private static bool IsInteracSubmission(string? reference) =>
        reference?.Contains(InteracReferenceMarker, StringComparison.Ordinal) == true;

    private async Task WriteAuditAsync(Payment payment, Guid? adminId, string adminName, string action, string details)
    {
        try
        {
            await _auditLogRepository.AddAsync(new AuditLog(
                action: action,
                entityType: nameof(Payment),
                entityId: payment.Id,
                details: details,
                userId: adminId,
                userName: adminName));
        }
        catch (Exception ex)
        {
            // The status change already happened; a missing audit row must not report it as an error.
            _logger.LogWarning(ex, "Audit entry failed for reconciled payment {PaymentId}", payment.Id);
        }
    }
}
