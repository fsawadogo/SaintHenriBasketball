using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto createPaymentDto)
    {
        var user = await _userRepository.GetByIdAsync(createPaymentDto.UserId);
        if (user == null)
        {
            throw new NotFoundException($"User with ID {createPaymentDto.UserId} not found");
        }

        var payment = new Payment(createPaymentDto.UserId, createPaymentDto.Amount, createPaymentDto.Plan);
        await _paymentRepository.AddAsync(payment);

        _logger.LogInformation("Payment created for user {UserId}", createPaymentDto.UserId);

        return _mapper.Map<PaymentDto>(payment);
    }

    public async Task<PaymentDto> GetPaymentAsync(Guid id)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);
        return _mapper.Map<PaymentDto>(payment);
    }

    public async Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(Guid userId)
    {
        var payments = await _paymentRepository.GetPaymentsByUserAsync(userId);
        return _mapper.Map<IEnumerable<PaymentDto>>(payments);
    }

    public async Task UpdatePaymentStatusAsync(Guid id, PaymentStatus status)
    {
        var payment = await _paymentRepository.GetByIdAsync(id);
        if (payment == null)
        {
            throw new NotFoundException($"Payment with ID {id} not found");
        }

        payment.Status = status;
        await _paymentRepository.UpdateAsync(payment);

        _logger.LogInformation("Payment {PaymentId} status updated to {Status}", id, status);
    }

    public async Task<PaymentSummaryDto> GetPaymentSummaryAsync()
    {
        var payments = await _paymentRepository.GetAllAsync();
        
        return new PaymentSummaryDto
        {
            TotalPayments = payments.Count(),
            TotalAmount = payments.Sum(p => p.Amount),
            SeasonPayments = payments.Count(p => p.Plan == PaymentPlan.Season),
            DropInPayments = payments.Count(p => p.Plan == PaymentPlan.DropIn),
            SeasonRevenue = payments.Where(p => p.Plan == PaymentPlan.Season).Sum(p => p.Amount),
            DropInRevenue = payments.Where(p => p.Plan == PaymentPlan.DropIn).Sum(p => p.Amount)
        };
    }

    public async Task<IEnumerable<PaymentDto>> GetPendingPaymentsAsync()
    {
        var payments = await _paymentRepository.GetPaymentsByStatusAsync(PaymentStatus.Pending);
        return _mapper.Map<IEnumerable<PaymentDto>>(payments);
    }

    public async Task<IEnumerable<PaymentDto>> GetAllPayments()
    {
        var payments = _paymentRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<PaymentDto>>(payments);
    }
}