using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Reconciliation;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class ReconciliationService : IReconciliationService
{
    private const int StaleThresholdDays = 7;

    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        IPaymentRepository paymentRepository,
        IPaymentService paymentService,
        ILogger<ReconciliationService> logger)
    {
        _paymentRepository = paymentRepository;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PendingPaymentDto>> GetPendingAsync()
    {
        var pending = await _paymentRepository.GetPaymentsByStatusAsync(PaymentStatus.Pending);
        var now = DateTime.UtcNow;

        return pending.Select(p =>
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

    public async Task<BulkCompletePaymentsResultDto> BulkCompleteAsync(IEnumerable<Guid> paymentIds)
    {
        var result = new BulkCompletePaymentsResultDto();
        foreach (var id in paymentIds.Distinct())
        {
            try
            {
                await _paymentService.UpdatePaymentStatusAsync(id, PaymentStatus.Completed);
                result.Completed++;
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
}
