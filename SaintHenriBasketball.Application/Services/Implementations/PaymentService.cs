using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Email;
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
   private readonly IEmailService _emailService;

   public PaymentService(
       IPaymentRepository paymentRepository,
       IUserRepository userRepository,
       IMapper mapper,
       ILogger<PaymentService> logger,
       IEmailService emailService)
   {
       _paymentRepository = paymentRepository;
       _userRepository = userRepository;
       _mapper = mapper;
       _logger = logger;
       _emailService = emailService;
   }

   public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto createPaymentDto)
   {
       var user = await _userRepository.GetByIdAsync(createPaymentDto.UserId);
       if (user == null)
           throw new NotFoundException($"User with ID {createPaymentDto.UserId} not found");

       var payment = new Payment(createPaymentDto.UserId, createPaymentDto.Amount, createPaymentDto.Plan);
       
       var reference = user.PaymentPlan == PaymentPlan.Season 
           ? $"SEASON-{DateTime.Now:yyMM}-{Random.Shared.Next(1000, 9999)}"
           : $"DROPIN-{DateTime.Now:yyMM}-{Random.Shared.Next(1000, 9999)}";
       
       payment.Reference = reference;
       
       await _paymentRepository.AddAsync(payment);

       _logger.LogInformation("Payment created for user {UserId}", createPaymentDto.UserId);

       await _emailService.SendPaymentCreatedConfirmationAsync(
           user.Email,
           payment.Amount,
           payment.Reference,
           user.PreferredLanguage
       );

       return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<PaymentDto> GetPaymentAsync(Guid id)
   {
       var payment = await _paymentRepository.GetByIdAsync(id);
       if (payment == null)
           throw new NotFoundException($"Payment with ID {id} not found");
           
       return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(Guid userId)
   {
       var user = await _userRepository.GetByIdAsync(userId);
       if (user == null)
           throw new NotFoundException($"User with ID {userId} not found");

       var payments = await _paymentRepository.GetPaymentsByUserAsync(userId);
       return _mapper.Map<IEnumerable<PaymentDto>>(payments);
   }

   public async Task<PaymentDto> UpdatePaymentStatusAsync(Guid id, PaymentStatus status)
   {
       var payment = await _paymentRepository.GetByIdAsync(id);
       if (payment == null)
           throw new NotFoundException($"Payment with ID {id} not found");

       payment.Status = status;
       await _paymentRepository.UpdateAsync(payment);
       _logger.LogInformation("Payment {PaymentId} status updated to {Status}", id, status);
       
       return _mapper.Map<PaymentDto>(payment);
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
       var payments = await _paymentRepository.GetAllAsync();
       return _mapper.Map<IEnumerable<PaymentDto>>(payments);
   }

   public async Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto createPaymentDto)
   {
       var user = await _userRepository.GetByIdAsync(createPaymentDto.UserId);
       if (user == null)
           throw new NotFoundException($"User with ID {createPaymentDto.UserId} not found");

       var payment = new Payment(createPaymentDto.UserId, createPaymentDto.Amount, createPaymentDto.Plan);
       
       try
       {
           payment.Status = PaymentStatus.Completed;
           await _paymentRepository.AddAsync(payment);

           await _emailService.SendPaymentConfirmationAsync(
               user.Email,
               payment.Amount,
               payment.Reference,
               user.PreferredLanguage
           );
       }
       catch (Exception ex)
       {
           payment.Status = PaymentStatus.Failed;
           await _paymentRepository.AddAsync(payment);
           
           await _emailService.SendGeneralAnnouncementEmailAsync(
               user.Email,
               user.FirstName + " " + user.LastName,
               $"Payment failed: {ex.Message}"
           );
           
           throw;
       }

       return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<PaymentReconciliationDto> ReconcilePaymentsAsync(DateTime startDate, DateTime endDate)
   {
       var payments = await _paymentRepository.GetPaymentsByDateRangeAsync(startDate, endDate);
       
       return new PaymentReconciliationDto
       {
           StartDate = startDate,
           EndDate = endDate,
           TotalPayments = payments.Count(),
           TotalAmount = payments.Sum(p => p.Amount),
           CompletedPayments = payments.Count(p => p.Status == PaymentStatus.Completed),
           CompletedAmount = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
           PendingPayments = payments.Count(p => p.Status == PaymentStatus.Pending),
           PendingAmount = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
           FailedPayments = payments.Count(p => p.Status == PaymentStatus.Failed),
           FailedAmount = payments.Where(p => p.Status == PaymentStatus.Failed).Sum(p => p.Amount),
           PaymentsByPlan = payments.GroupBy(p => p.Plan)
               .ToDictionary(g => g.Key, g => g.Count())
       };
   }

   public async Task<PaymentDto> UpdatePaymentAsync(Guid id, UpdatePaymentDto updatePaymentDto)
   {
        var payment = await _paymentRepository.GetByIdAsync(id);
       if (payment == null)
           throw new NotFoundException($"Payment with ID {id} not found");

        payment.Amount = updatePaymentDto.Amount;
        payment.Plan = updatePaymentDto.Plan;
        await _paymentRepository.UpdateAsync(payment);
                
        return _mapper.Map<PaymentDto>(payment);
   }

   private async Task SendPaymentStatusUpdateEmail(Payment payment, ApplicationUser user, PaymentStatus previousStatus)
   {
       if (payment.Status == PaymentStatus.Completed && previousStatus != PaymentStatus.Completed)
       {
           await _emailService.SendPaymentConfirmationAsync(
               user.Email,
               payment.Amount,
               payment.Reference,
               user.PreferredLanguage
           );
       }
       else if (payment.Status == PaymentStatus.Failed)
       {
           await _emailService.SendGeneralAnnouncementEmailAsync(
               user.Email,
               user.FirstName + " " + user.LastName,
               $"Your payment of ${payment.Amount} has failed. Please try again or contact support."
           );
       }
   }
}