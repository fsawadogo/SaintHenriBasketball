using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;
public class PaymentService : IPaymentService
{
   private readonly IPaymentRepository _paymentRepository;
   private readonly ISeasonRepository _seasonRepository;
   private readonly IUserRepository _userRepository;
   private readonly ISessionRepository _sessionRepository;
   private readonly ISessionRegistrationRepository _registrationRepository;
   private readonly IMapper _mapper;
   private readonly ILogger<PaymentService> _logger;
   private readonly IEmailService _emailService;
   private readonly INotificationService _notificationService;

   public PaymentService(
       IPaymentRepository paymentRepository,
       IUserRepository userRepository,
       ISessionRepository sessionRepository,
       ISessionRegistrationRepository registrationRepository,
       IMapper mapper,
       ILogger<PaymentService> logger,
       IEmailService emailService,
       INotificationService notificationService,
       ISeasonRepository seasonRepository)
   {
       _paymentRepository = paymentRepository;
       _seasonRepository = seasonRepository;
       _userRepository = userRepository;
       _sessionRepository = sessionRepository;
       _registrationRepository = registrationRepository;
       _mapper = mapper;
       _logger = logger;
       _emailService = emailService;
       _notificationService = notificationService;
   }

   public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto createPaymentDto)
   {
       var user = await _userRepository.GetByIdAsync(createPaymentDto.UserId);
       if (user == null)
           throw new NotFoundException($"User with ID {createPaymentDto.UserId} not found");

       await ValidateSeasonAsync(createPaymentDto.Plan, createPaymentDto.SeasonId);
       var payment = new Payment(createPaymentDto.UserId, createPaymentDto.Amount, createPaymentDto.Plan) { SeasonId = createPaymentDto.SeasonId };
       
       var reference = createPaymentDto.Plan == PaymentPlan.Season
           ? $"SEASON-{DateTime.UtcNow:yyMM}-{Random.Shared.Next(1000, 9999)}"
           : $"DROPIN-{DateTime.UtcNow:yyMM}-{Random.Shared.Next(1000, 9999)}";
       
       payment.Reference = reference;
       
       await _paymentRepository.AddAsync(payment);

       _logger.LogInformation("Payment created for user {UserId}", createPaymentDto.UserId);

       await _emailService.SendPaymentCreatedConfirmationAsync(
           user.Id,
           payment.Amount,
           payment.Reference);

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

       var previousStatus = payment.Status;
       payment.Status = status;
       // PaymentDate is when the club received the money; receipts and revenue reports group by it.
       if (status == PaymentStatus.Completed && previousStatus != PaymentStatus.Completed)
           payment.PaymentDate = DateTime.UtcNow;
       await _paymentRepository.UpdateAsync(payment);
       _logger.LogInformation("Payment {PaymentId} status updated to {Status}", id, status);

       if (previousStatus != status)
       {
           var user = await _userRepository.GetByIdAsync(payment.UserId);
           if (user != null)
               await NotifyPaymentStatusChangeAsync(user, payment);
           else
               _logger.LogWarning(
                   "Cannot send payment status notification: user {UserId} not found", payment.UserId);
       }

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

       await ValidateSeasonAsync(createPaymentDto.Plan, createPaymentDto.SeasonId);
       var payment = new Payment(createPaymentDto.UserId, createPaymentDto.Amount, createPaymentDto.Plan) { SeasonId = createPaymentDto.SeasonId };

       try
       {
           payment.Status = PaymentStatus.Completed;
           await _paymentRepository.AddAsync(payment);
           await NotifyPaymentStatusChangeAsync(user, payment);
       }
       catch (Exception ex)
       {
           payment.Status = PaymentStatus.Failed;
           await _paymentRepository.AddAsync(payment);
           await NotifyPaymentStatusChangeAsync(user, payment, ex.Message);
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

        await ValidateSeasonAsync(updatePaymentDto.Plan, updatePaymentDto.SeasonId);
        if (payment.SessionId != null && updatePaymentDto.Plan == PaymentPlan.Season)
            throw new ValidationException("A session payment cannot be converted into a season payment.");
        payment.SeasonId = updatePaymentDto.SeasonId;
        var previousStatus = payment.Status;
        payment.Amount = updatePaymentDto.Amount;
        payment.Plan = updatePaymentDto.Plan;
        payment.Status = updatePaymentDto.Status;
        await _paymentRepository.UpdateAsync(payment);

        if (previousStatus != payment.Status)
        {
            var user = await _userRepository.GetByIdAsync(payment.UserId);
            if (user != null)
                await NotifyPaymentStatusChangeAsync(user, payment);
            else
                _logger.LogWarning(
                    "Cannot send payment status notification: user {UserId} not found", payment.UserId);
        }

        return _mapper.Map<PaymentDto>(payment);
   }

   private async Task NotifyPaymentStatusChangeAsync(ApplicationUser user, Payment payment, string? failureReason = null)
   {
       try
       {
           switch (payment.Status)
           {
               case PaymentStatus.Completed:
                   await _emailService.SendPaymentConfirmationAsync(
                       user, payment.Amount, payment.Reference);
                   await _notificationService.CreateAsync(
                       user.Id,
                       Domain.Entities.NotificationType.PaymentCompleted,
                       title: "Payment confirmed",
                       body: $"Your payment of ${payment.Amount:F2} was confirmed. Thanks!",
                       url: "/payment-history");
                   break;

               case PaymentStatus.Pending:
                   await _emailService.SendPaymentReminderEmailAsync(user, user.PaymentPlan);
                   break;

               case PaymentStatus.Failed:
                   await _emailService.SendPaymentFailedAsync(
                       user, payment.Amount, payment.Reference, failureReason);
                   break;
           }
       }
       catch (Exception ex)
       {
           _logger.LogError(ex,
               "Failed to send payment status change notification for payment {PaymentId}", payment.Id);
       }
   }

   public async Task<PaymentDto> CreateDropInPaymentAsync(Guid userId, CreateDropInPaymentDto request)
   {
       var user = await _userRepository.GetByIdAsync(userId);
       if (user == null)
           throw new NotFoundException($"User with ID {userId} not found");

       var session = await _sessionRepository.GetByIdAsync(request.SessionId);
       if (session == null)
           throw new NotFoundException($"Session with ID {request.SessionId} not found");

       if (request.PaymentMethod is < 0 or > 2)
           throw new ValidationException("Unsupported payment method");
       if (!await _registrationRepository.IsUserRegisteredAsync(userId, request.SessionId))
           throw new ValidationException("Reserve your place before paying");
       if (session.Status == SessionStatus.Cancelled)
           throw new ValidationException("This session is cancelled");
       if (session.DropInPrice <= 0)
           throw new ValidationException("No payment is required for this session");
       if (request.PaymentMethod == 0 && (string.IsNullOrWhiteSpace(request.InteracReference) || request.InteracReference.Trim().Length > 80))
           throw new ValidationException("Enter a bank confirmation number of up to 80 characters");

       var (payment, created) = await _paymentRepository.GetOrCreateSessionPaymentAsync(userId, request.SessionId, session.DropInPrice);
       if (payment.Status != PaymentStatus.Pending)
           throw new ValidationException("This payment is not pending. Check your payment history.");
       if (request.PaymentMethod == 0)
           return await ConfirmInteracPaymentAsync(payment.Id, request.InteracReference!);
       if (payment.Reference?.Contains("|INTERAC:") == true)
           throw new ValidationException("Your Interac transfer is awaiting verification. Do not pay twice.");

       return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<PaymentDto> CreateSeasonPaymentAsync(Guid userId, CreateSeasonPaymentDto request)
   {
       var user = await _userRepository.GetByIdAsync(userId);
       if (user == null) throw new NotFoundException($"User with ID {userId} not found");
       if (user.PaymentPlan != PaymentPlan.Season) throw new ValidationException("Choose the Season plan before paying season fees.");
       var season = await _seasonRepository.GetByIdAsync(request.SeasonId);
       if (season == null) throw new NotFoundException($"Season with ID {request.SeasonId} not found");
       if (season.Price <= 0) throw new ValidationException("No payment is required for this season.");
       if (request.PaymentMethod != 0) throw new ValidationException("Interac is currently the available season payment method.");
       if (string.IsNullOrWhiteSpace(request.InteracReference) || request.InteracReference.Trim().Length > 80)
           throw new ValidationException("Enter a bank confirmation number of up to 80 characters.");

       var (payment, _) = await _paymentRepository.GetOrCreateSeasonPaymentAsync(userId, request.SeasonId, season.Price);
       if (payment.Status == PaymentStatus.Completed) return _mapper.Map<PaymentDto>(payment);
       if (payment.Status != PaymentStatus.Pending) throw new ValidationException("This season payment cannot be submitted. Contact the club.");
       return await ConfirmInteracPaymentAsync(payment.Id, request.InteracReference);
   }

   public async Task<PaymentDto> ConfirmInteracPaymentAsync(Guid paymentId, string reference)
   {
       var payment = await _paymentRepository.GetByIdAsync(paymentId);
       if (payment == null)
           throw new NotFoundException($"Payment with ID {paymentId} not found");

       if (payment.Status != PaymentStatus.Pending)
           throw new ValidationException("Only pending payments can be confirmed");

       reference = reference?.Trim() ?? "";
       if (reference.Length == 0 || reference.Length > 80 || reference.Any(char.IsControl) || reference.Contains('|'))
           throw new ValidationException("Enter a valid bank confirmation number of up to 80 characters");
       if (payment.Reference?.StartsWith("cs_") == true)
           throw new ValidationException("Card checkout has already started. Contact the club before sending an Interac transfer.");
       if (payment.Reference?.Contains("|INTERAC:") == true)
       {
           if (payment.Reference.EndsWith("|INTERAC:" + reference, StringComparison.Ordinal))
               return _mapper.Map<PaymentDto>(payment);
           throw new ValidationException("A reference has already been submitted. Contact the club to correct it.");
       }
       var newReference = $"{payment.Reference}|INTERAC:{reference}";
       if (!await _paymentRepository.TrySetPendingReferenceAsync(payment.Id, payment.Reference, newReference))
           throw new ValidationException("The payment changed. Refresh your payment history before retrying.");
       payment.Reference = newReference;

       _logger.LogInformation(
           "Interac reference {Reference} attached to payment {PaymentId}", reference, paymentId);

       return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<DropInPaymentLinkDto> GetDropInPaymentLinkAsync(Guid userId, Guid sessionId)
   {
       var session = await _sessionRepository.GetByIdAsync(sessionId);
       if (session == null)
           throw new NotFoundException($"Session with ID {sessionId} not found");

       var amount = session.DropInPrice;

       // Check if there's already a pending payment for this user+session
       var existingPayment = await _paymentRepository.GetByUserAndSessionAsync(userId, sessionId);

       return new DropInPaymentLinkDto
       {
           PaymentId = existingPayment?.Id ?? Guid.Empty,
           SessionId = sessionId,
           Amount = amount,
           PaymentUrl = $"https://sainthenribasketball.com/drop-in-payment?sessionId={sessionId}",
           InteracEmail = "pay@sainthenribasketball.com",
           ExpiresAt = session.SessionDate
       };
   }

   public async Task<(Guid Id, bool Created)?> EnsureDropInPaymentForSessionAsync(Guid userId, Guid sessionId)
   {
       var user = await _userRepository.GetByIdAsync(userId);
       if (user is null || user.PaymentPlan != PaymentPlan.DropIn) return null;

       var session = await _sessionRepository.GetByIdAsync(sessionId);
       if (session is null) return null;

       // Compare in local (Montreal) time so a Saturday session doesn't get billed on
       // Friday-evening UTC ticks.
       var todayLocal = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
       if (session.SessionDate.Date != todayLocal) return null;

       if (!await _registrationRepository.IsUserRegisteredAsync(userId, sessionId)) return null;

       return await EnsureCoreAsync(user, session, registrationConfirmed: true);
   }

   public async Task<int> RunDailyDropInBillingAsync()
   {
       var todayLocal = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;

       var upcoming = await _sessionRepository.GetUpcomingSessionsAsync();
       var todays = upcoming
           .Where(s => s.Status == SessionStatus.Open && s.SessionDate.Date == todayLocal)
           .ToList();

       if (todays.Count == 0)
       {
           _logger.LogInformation("DropInBilling: no open sessions today; nothing to do");
           return 0;
       }

       var created = 0;
       foreach (var session in todays)
       {
           var registrations = await _registrationRepository.GetBySessionIdAsync(session.Id);
           if (registrations.Count == 0) continue;

           // Bulk-fetch users in one query instead of N round-trips.
           var userIds = registrations.Select(r => r.UserId).Distinct().ToList();
           var users = (await _userRepository.GetUsersByIdsAsync(userIds)).ToDictionary(u => u.Id);

           foreach (var reg in registrations)
           {
               if (!users.TryGetValue(reg.UserId, out var user)) continue;
               if (user.PaymentPlan != PaymentPlan.DropIn) continue;

               var result = await EnsureCoreAsync(user, session, registrationConfirmed: true);
               if (result.Created) created++;
           }
       }

       _logger.LogInformation("DropInBilling run complete: {Sessions} session(s), {Created} new payments", todays.Count, created);
       return created;
   }

   // Shared core: callers must have already verified user is on DropIn plan and the
   // session is "today". `registrationConfirmed` lets the cron path skip a redundant
   // IsUserRegisteredAsync query (it already loaded `registrations`).
   private async Task<(Guid Id, bool Created)> EnsureCoreAsync(ApplicationUser user, Session session, bool registrationConfirmed)
   {
       if (!registrationConfirmed && !await _registrationRepository.IsUserRegisteredAsync(user.Id, session.Id))
           throw new InvalidOperationException($"User {user.Id} not registered for session {session.Id}");

       if (session.DropInPrice <= 0) return (Guid.Empty, false);
       var (payment, created) = await _paymentRepository.GetOrCreateSessionPaymentAsync(user.Id, session.Id, session.DropInPrice);
       if (!created) return (payment.Id, false);

       _logger.LogInformation("Auto-billed drop-in payment {PaymentId} for user {UserId} session {SessionId}",
           payment.Id, user.Id, session.Id);

       try
       {
           await _emailService.SendPaymentCreatedConfirmationAsync(user.Id, payment.Amount, payment.Reference);
       }
       catch (Exception ex)
       {
           _logger.LogWarning(ex, "Auto-bill email failed for payment {PaymentId} — payment row still created", payment.Id);
       }

       return (payment.Id, true);
   }
   private async Task ValidateSeasonAsync(PaymentPlan plan, Guid? seasonId)
   {
       if (plan == PaymentPlan.Season)
       {
           if (seasonId == null || await _seasonRepository.GetByIdAsync(seasonId.Value) == null)
               throw new ValidationException("Select a valid season for a season payment.");
       }
       else if (seasonId != null)
           throw new ValidationException("Drop-in payments cannot be linked to a season.");
   }

}
