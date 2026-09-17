using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;
public class PaymentService : IPaymentService
{
   private const string InteracReferenceRequiredMessage = "Enter a bank confirmation number of up to 80 characters";

   private readonly IPaymentRepository _paymentRepository;
   private readonly ISeasonRepository _seasonRepository;
   private readonly IUserRepository _userRepository;
   private readonly ISessionRepository _sessionRepository;
   private readonly ISessionRegistrationRepository _registrationRepository;
   private readonly IMapper _mapper;
   private readonly ILogger<PaymentService> _logger;
   private readonly IEmailService _emailService;
   private readonly INotificationService _notificationService;
   private readonly IPromoCodeRepository _promoCodeRepository;
   private readonly IAccountCreditRepository _accountCreditRepository;
   private readonly IReferralService _referralService;
   private readonly IFeatureFlagService _featureFlagService;
   private readonly ISeasonPlanChoiceRepository _seasonPlanChoiceRepository;

   public PaymentService(
       IPaymentRepository paymentRepository,
       IUserRepository userRepository,
       ISessionRepository sessionRepository,
       ISessionRegistrationRepository registrationRepository,
       IMapper mapper,
       ILogger<PaymentService> logger,
       IEmailService emailService,
       INotificationService notificationService,
       ISeasonRepository seasonRepository,
       IPromoCodeRepository promoCodeRepository,
       IAccountCreditRepository accountCreditRepository,
       IReferralService referralService,
       IFeatureFlagService featureFlagService,
       ISeasonPlanChoiceRepository seasonPlanChoiceRepository)
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
       _promoCodeRepository = promoCodeRepository;
       _accountCreditRepository = accountCreditRepository;
       _referralService = referralService;
       _featureFlagService = featureFlagService;
       _seasonPlanChoiceRepository = seasonPlanChoiceRepository;
   }

   public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto createPaymentDto)
   {
       var user = await _userRepository.GetByIdAsync(createPaymentDto.UserId);
       if (user == null)
           throw new NotFoundException($"User with ID {createPaymentDto.UserId} not found");

       if (createPaymentDto.Amount < 0)
           throw new ValidationException(PaymentStatusRules.NegativeAmountMessage);
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
       if (!PaymentStatusRules.CanTransition(previousStatus, status))
           throw new ValidationException(PaymentStatusRules.TransitionMessage(previousStatus, status));
       ReopenFailedPayment(payment, previousStatus, status);
       payment.Status = status;
       // PaymentDate is when the club received the money; receipts and revenue reports group by it.
       if (status == PaymentStatus.Completed && previousStatus != PaymentStatus.Completed)
           payment.PaymentDate = DateTime.UtcNow;
       await _paymentRepository.UpdateAsync(payment);
       _logger.LogInformation("Payment {PaymentId} status updated to {Status}", id, status);

       await OnStatusSavedAsync(payment, previousStatus);

       return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<PaymentSummaryDto> GetPaymentSummaryAsync()
   {
       var payments = await _paymentRepository.GetAllAsync();

       // Revenue is money actually collected: pending, failed and refunded payments don't count.
       var collected = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
       return new PaymentSummaryDto
       {
           TotalPayments = payments.Count(),
           TotalAmount = collected.Sum(p => p.Amount),
           SeasonPayments = payments.Count(p => p.Plan == PaymentPlan.Season),
           DropInPayments = payments.Count(p => p.Plan == PaymentPlan.DropIn),
           SeasonRevenue = collected.Where(p => p.Plan == PaymentPlan.Season).Sum(p => p.Amount),
           DropInRevenue = collected.Where(p => p.Plan == PaymentPlan.DropIn).Sum(p => p.Amount)
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

   public const string SearchDateRangeMessage = "The start date must be on or before the end date.";

   public async Task<PaymentSearchResultDto> SearchPaymentsAsync(PaymentSearchCriteria criteria)
   {
       if (criteria.From is DateTime from && criteria.To is DateTime to && from > to)
           throw new ValidationException(SearchDateRangeMessage);
       var (page, pageSize) = ListPaging.Clamp(criteria.Page, criteria.PageSize);
       var result = await _paymentRepository.SearchAsync(criteria with
       {
           Search = string.IsNullOrWhiteSpace(criteria.Search) ? null : criteria.Search.Trim(),
           Page = page,
           PageSize = pageSize,
       });
       return new PaymentSearchResultDto
       {
           Items = _mapper.Map<List<PaymentDto>>(result.Items),
           Total = result.Totals.Count,
           Page = page,
           PageSize = pageSize,
           Summary = new PaymentSearchSummaryDto
           {
               CompletedCount = result.Totals.CompletedCount,
               Collected = result.Totals.Collected,
               SeasonCollected = result.Totals.SeasonCollected,
               DropInCollected = result.Totals.DropInCollected,
           },
       };
   }

   public async Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto createPaymentDto)
   {
       var user = await _userRepository.GetByIdAsync(createPaymentDto.UserId);
       if (user == null)
           throw new NotFoundException($"User with ID {createPaymentDto.UserId} not found");

       if (createPaymentDto.Amount < 0)
           throw new ValidationException(PaymentStatusRules.NegativeAmountMessage);
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
        var previousStatus = payment.Status;
        if (!PaymentStatusRules.CanTransition(previousStatus, updatePaymentDto.Status))
            throw new ValidationException(PaymentStatusRules.TransitionMessage(previousStatus, updatePaymentDto.Status));
        if (updatePaymentDto.Amount < 0)
            throw new ValidationException(PaymentStatusRules.NegativeAmountMessage);
        // Money already received, failed or given back is a record; only an open charge can be corrected.
        var changesCharge = payment.Amount != updatePaymentDto.Amount
            || payment.Plan != updatePaymentDto.Plan
            || payment.SeasonId != updatePaymentDto.SeasonId;
        if (changesCharge && previousStatus != PaymentStatus.Pending)
            throw new ValidationException(PaymentStatusRules.AmountLockedMessage);
        payment.SeasonId = updatePaymentDto.SeasonId;
        // The admin edits the charged amount; keep Amount = OriginalAmount - DiscountAmount - CreditApplied true.
        if (payment.OriginalAmount != null && payment.Amount != updatePaymentDto.Amount)
            payment.OriginalAmount = updatePaymentDto.Amount + payment.DiscountAmount + payment.CreditApplied;
        payment.Amount = updatePaymentDto.Amount;
        payment.Plan = updatePaymentDto.Plan;
        ReopenFailedPayment(payment, previousStatus, updatePaymentDto.Status);
        payment.Status = updatePaymentDto.Status;
        if (payment.Status == PaymentStatus.Completed && previousStatus != PaymentStatus.Completed)
            payment.PaymentDate = DateTime.UtcNow;
        await _paymentRepository.UpdateAsync(payment);

        await OnStatusSavedAsync(payment, previousStatus);

        return _mapper.Map<PaymentDto>(payment);
   }

   public async Task<bool> VoidForCancelledSessionAsync(Guid paymentId)
   {
       var payment = await _paymentRepository.GetByIdAsync(paymentId);
       if (payment == null || payment.Status != PaymentStatus.Pending) return false;
       payment.Status = PaymentStatus.Failed;
       await _paymentRepository.UpdateAsync(payment);
       if (payment.CreditApplied > 0)
           await ReleaseCreditAsync(payment);
       _logger.LogInformation("Payment {PaymentId} voided because its session was cancelled", paymentId);
       return true;
   }

   /// Reopening a failed payment: its account credit was already given back, so the full charge returns.
   private static void ReopenFailedPayment(Payment payment, PaymentStatus from, PaymentStatus to)
   {
       if (from != PaymentStatus.Failed || to != PaymentStatus.Pending || payment.CreditApplied <= 0) return;
       payment.Amount += payment.CreditApplied;
       payment.CreditApplied = 0;
   }

   /// <summary>
   /// Side effects of a saved status. Ledger effects run on every save in the matching status —
   /// both are idempotent — so re-saving the status retries one that failed; notifications are
   /// sent only when the status actually changed.
   /// </summary>
   private async Task OnStatusSavedAsync(Payment payment, PaymentStatus previousStatus)
   {
       if (payment.Status == PaymentStatus.Completed)
           await _referralService.GrantRewardForPaymentAsync(payment.UserId, payment.Id, payment.Amount, payment.PaymentDate);
       else if (payment.Status is PaymentStatus.Failed or PaymentStatus.Refunded && payment.CreditApplied > 0)
           await ReleaseCreditAsync(payment);

       if (previousStatus == payment.Status) return;
       var user = await _userRepository.GetByIdAsync(payment.UserId);
       if (user != null)
           await NotifyPaymentStatusChangeAsync(user, payment);
       else
           _logger.LogWarning(
               "Cannot send payment status notification: user {UserId} not found", payment.UserId);
   }

   /// Gives back the credit a failed or refunded payment consumed. The promo use is not returned.
   private async Task ReleaseCreditAsync(Payment payment)
   {
       try
       {
           var released = await _accountCreditRepository.TryAddAsync(
               new AccountCredit(payment.UserId, payment.CreditApplied, AccountCreditKind.Released, paymentId: payment.Id));
           if (released)
               _logger.LogInformation("Released {Credit} credit from payment {PaymentId} ({Status})",
                   payment.CreditApplied, payment.Id, payment.Status);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Failed to release credit for payment {PaymentId}; re-save its status to retry", payment.Id);
       }
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

               case PaymentStatus.Refunded:
                   await _notificationService.CreateAsync(
                       user.Id,
                       Domain.Entities.NotificationType.Generic,
                       title: "Payment refunded",
                       body: payment.RefundMethod == RefundMethod.AccountCredit
                           ? $"Your payment of ${payment.Amount:F2} was refunded as account credit."
                           : $"Your payment of ${payment.Amount:F2} was refunded.",
                       url: "/payment-history");
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
       var interacReference = request.InteracReference?.Trim();
       if (request.PaymentMethod == 0)
       {
           if (interacReference?.Length > 80)
               throw new ValidationException(InteracReferenceRequiredMessage);
           // Credit or a promo can bring the total to zero, in which case there is nothing to transfer.
           if (string.IsNullOrEmpty(interacReference))
           {
               var existing = await _paymentRepository.GetByUserAndSessionAsync(userId, request.SessionId);
               await EnsureNothingToTransferAsync(userId, PaymentPlan.DropIn, existing, session.DropInPrice, request.PromoCode, InteracReferenceRequiredMessage);
           }
       }
       else
       {
           // Checked read-only first so no promo use is reserved and no credit is debited for a charge Stripe would refuse.
           var quote = await GetQuoteAsync(userId, new PaymentQuoteRequestDto { Plan = PaymentPlan.DropIn, SessionId = request.SessionId, PromoCode = request.PromoCode });
           if (PaymentPricing.IsBelowCardMinimum(quote.Total))
               throw new ValidationException(PaymentPricing.BelowCardMinimumMessage);
       }

       var (payment, created) = await _paymentRepository.GetOrCreateSessionPaymentAsync(userId, request.SessionId, session.DropInPrice);
       if (payment.Status != PaymentStatus.Pending)
           throw new ValidationException("This payment is not pending. Check your payment history.");

       await ApplyAdjustmentsAsync(payment, request.PromoCode);
       if (IsSettledByAdjustments(payment))
           return await UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Completed);
       // Credit can change between the quote and the adjustment; never hand Stripe a sub-minimum charge.
       if (request.PaymentMethod != 0 && PaymentPricing.IsBelowCardMinimum(payment.Amount))
           throw new ValidationException(PaymentPricing.BelowCardMinimumMessage);

       if (request.PaymentMethod == 0)
       {
           if (string.IsNullOrEmpty(interacReference))
               throw new ValidationException(InteracReferenceRequiredMessage);
           return await ConfirmInteracPaymentAsync(payment.Id, interacReference);
       }
       if (payment.Reference?.Contains("|INTERAC:") == true)
           throw new ValidationException("Your Interac transfer is awaiting verification. Do not pay twice.");

       return _mapper.Map<PaymentDto>(await _paymentRepository.GetByIdAsync(payment.Id));
   }

   public async Task<PaymentDto> CreateSeasonPaymentAsync(Guid userId, CreateSeasonPaymentDto request)
   {
       const string seasonReferenceMessage = "Enter a bank confirmation number of up to 80 characters.";
       var user = await _userRepository.GetByIdAsync(userId);
       if (user == null) throw new NotFoundException($"User with ID {userId} not found");
       if (user.PaymentPlan != PaymentPlan.Season) throw new ValidationException("Choose the Season plan before paying season fees.");
       var season = await _seasonRepository.GetByIdAsync(request.SeasonId);
       if (season == null) throw new NotFoundException($"Season with ID {request.SeasonId} not found");
       if (season.Price <= 0) throw new ValidationException("No payment is required for this season.");
       var card = request.PaymentMethod == 1;
       if (request.PaymentMethod != 0 && !(card && await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.SeasonCardPayments)))
           throw new ValidationException("Interac is currently the available season payment method.");
       var interacReference = card ? null : request.InteracReference?.Trim();
       if (interacReference?.Length > 80)
           throw new ValidationException(seasonReferenceMessage);
       if (card)
       {
           // Checked read-only first so no promo use is reserved and no credit is debited for a charge Stripe would refuse.
           var quote = await GetQuoteAsync(userId, new PaymentQuoteRequestDto { Plan = PaymentPlan.Season, SeasonId = request.SeasonId, PromoCode = request.PromoCode });
           if (PaymentPricing.IsBelowCardMinimum(quote.Total))
               throw new ValidationException(PaymentPricing.BelowCardMinimumMessage);
       }
       else if (string.IsNullOrEmpty(interacReference))
       {
           var existing = await _paymentRepository.GetByUserAndSeasonAsync(userId, request.SeasonId);
           await EnsureNothingToTransferAsync(userId, PaymentPlan.Season, existing, season.Price, request.PromoCode, seasonReferenceMessage);
       }

       var (payment, _) = await _paymentRepository.GetOrCreateSeasonPaymentAsync(userId, request.SeasonId, season.Price);
       // Runs before the Completed short-circuit so a promo code sent for a settled payment is refused.
       await ApplyAdjustmentsAsync(payment, request.PromoCode);
       if (payment.Status == PaymentStatus.Completed) return _mapper.Map<PaymentDto>(await _paymentRepository.GetByIdAsync(payment.Id));
       if (payment.Status != PaymentStatus.Pending) throw new ValidationException("This season payment cannot be submitted. Contact the club.");
       if (IsSettledByAdjustments(payment))
           return await UpdatePaymentStatusAsync(payment.Id, PaymentStatus.Completed);
       if (card)
       {
           // Credit can change between the quote and the adjustment; never hand Stripe a sub-minimum charge.
           if (PaymentPricing.IsBelowCardMinimum(payment.Amount))
               throw new ValidationException(PaymentPricing.BelowCardMinimumMessage);
           if (payment.Reference?.Contains("|INTERAC:") == true)
               throw new ValidationException("Your Interac transfer is awaiting verification. Do not pay twice.");
           return _mapper.Map<PaymentDto>(await _paymentRepository.GetByIdAsync(payment.Id));
       }
       if (string.IsNullOrEmpty(interacReference)) throw new ValidationException(seasonReferenceMessage);
       return await ConfirmInteracPaymentAsync(payment.Id, interacReference);
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

   public async Task<PaymentQuoteDto> GetQuoteAsync(Guid userId, PaymentQuoteRequestDto request)
   {
       Payment? existing;
       decimal listPrice;
       switch (request.Plan)
       {
           case PaymentPlan.DropIn:
               if (request.SessionId is not Guid sessionId) throw new ValidationException("Select a session to quote.");
               var session = await _sessionRepository.GetByIdAsync(sessionId)
                   ?? throw new NotFoundException($"Session with ID {sessionId} not found");
               existing = await _paymentRepository.GetByUserAndSessionAsync(userId, sessionId);
               listPrice = session.DropInPrice;
               break;
           case PaymentPlan.Season:
               if (request.SeasonId is not Guid seasonId) throw new ValidationException("Select a season to quote.");
               var season = await _seasonRepository.GetByIdAsync(seasonId)
                   ?? throw new NotFoundException($"Season with ID {seasonId} not found");
               existing = await _paymentRepository.GetByUserAndSeasonAsync(userId, seasonId);
               listPrice = season.Price;
               break;
           default:
               throw new ValidationException("Unsupported payment plan.");
       }

       var pricing = await PriceAsync(userId, request.Plan, existing, listPrice, request.PromoCode);
       return new PaymentQuoteDto
       {
           OriginalAmount = pricing.OriginalAmount,
           DiscountAmount = pricing.DiscountAmount,
           CreditApplied = pricing.CreditApplied,
           Total = pricing.Total,
           PromoCode = pricing.AppliedCode,
           PromoError = pricing.PromoError,
           Locked = pricing.Locked,
       };
   }

   /// <param name="AppliedCode">Promo code the total includes (already on the payment, or newly valid).</param>
   /// <param name="ReservePromoCodeId">Set when applying the pricing must reserve a new promo use.</param>
   private sealed record Pricing(
       decimal OriginalAmount, decimal DiscountAmount, decimal CreditApplied, decimal Total,
       Guid? PromoCodeId, string? AppliedCode, Guid? ReservePromoCodeId, string? PromoError, bool Locked);

   /// <summary>
   /// Single source of the pricing rules; read-only. A locked payment keeps its stored values.
   /// Otherwise: base = the payment's OriginalAmount ?? Amount (or the list price when there is no
   /// payment yet), an already-applied promo and credit are kept, a new promo is evaluated only
   /// while the promo-codes flag is on, and credit = min(balance, base - discount).
   /// </summary>
   private async Task<Pricing> PriceAsync(Guid userId, PaymentPlan plan, Payment? payment, decimal listPrice, string? promoCode)
   {
       var code = PaymentPricing.NormalizeCode(promoCode);
       var existingCode = payment is null ? null : await GetAppliedPromoCodeAsync(payment);

       if (payment != null && PaymentPricing.IsLocked(payment))
       {
           return new Pricing(payment.OriginalAmount ?? payment.Amount, payment.DiscountAmount, payment.CreditApplied, payment.Amount,
               payment.PromoCodeId, existingCode, null,
               code != null && code != existingCode ? PaymentPricing.PaymentLockedMessage : null, Locked: true);
       }

       var original = payment?.OriginalAmount ?? payment?.Amount ?? listPrice;
       var discount = payment?.DiscountAmount ?? 0m;
       var promoCodeId = payment?.PromoCodeId;
       var appliedCode = existingCode;
       Guid? reserve = null;
       string? promoError = null;

       if (code != null && code != existingCode)
       {
           if (promoCodeId != null)
               promoError = PaymentPricing.PromoAlreadyAppliedMessage;
           else if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.PromoCodes))
               promoError = PaymentPricing.PromoCodesUnavailableMessage;
           else
           {
               var promo = await _promoCodeRepository.GetByCodeAsync(code);
               promoError = PaymentPricing.GetIneligibilityReason(promo, plan, DateTime.UtcNow);
               if (promoError == null)
               {
                   discount = PaymentPricing.CalculateDiscount(promo!, original);
                   promoCodeId = promo!.Id;
                   appliedCode = promo.Code;
                   reserve = promo.Id;
               }
           }
       }

       var credit = payment?.CreditApplied ?? 0m;
       if (credit == 0)
       {
           var balance = PaymentPricing.RoundCents(await _accountCreditRepository.GetBalanceAsync(userId));
           credit = Math.Clamp(balance, 0m, Math.Max(0m, original - discount));
       }

       var total = Math.Max(0m, PaymentPricing.RoundCents(original - discount - credit));
       return new Pricing(original, discount, credit, total, promoCodeId, appliedCode, reserve, promoError, Locked: false);
   }

   /// <summary>
   /// Applies the promo code and available credit to a payment the caller just got or created.
   /// Throws ValidationException when the promo cannot be used or the payment changed underneath;
   /// in that case nothing was written. On success <paramref name="payment"/> holds the new values.
   /// </summary>
   private async Task ApplyAdjustmentsAsync(Payment payment, string? promoCode)
   {
       var pricing = await PriceAsync(payment.UserId, payment.Plan, payment, payment.Amount, promoCode);
       if (pricing.PromoError != null) throw new ValidationException(pricing.PromoError);
       if (pricing.Locked) return;

       var unchanged = pricing.PromoCodeId == payment.PromoCodeId
           && pricing.DiscountAmount == payment.DiscountAmount
           && pricing.CreditApplied == payment.CreditApplied
           && pricing.Total == payment.Amount
           && pricing.OriginalAmount == (payment.OriginalAmount ?? payment.Amount);
       if (unchanged) return;

       var result = await _paymentRepository.TryApplyAdjustmentsAsync(payment, new PaymentAdjustment(
           pricing.OriginalAmount, pricing.DiscountAmount, pricing.CreditApplied,
           pricing.PromoCodeId, pricing.ReservePromoCodeId, pricing.Total));
       switch (result)
       {
           case PaymentAdjustmentResult.PromoUnavailable:
               throw new ValidationException(PaymentPricing.PromoUsageLimitMessage);
           case PaymentAdjustmentResult.PaymentChanged:
               throw new ValidationException(PaymentPricing.PaymentChangedMessage);
       }

       _logger.LogInformation(
           "Payment {PaymentId} adjusted: original {Original}, discount {Discount} (promo {PromoCode}), credit {Credit}, total {Total}",
           payment.Id, pricing.OriginalAmount, pricing.DiscountAmount, pricing.AppliedCode, pricing.CreditApplied, pricing.Total);
   }

   /// Rejects a request without an Interac reference unless the discount and credit cover the whole price.
   private async Task EnsureNothingToTransferAsync(Guid userId, PaymentPlan plan, Payment? existing, decimal listPrice, string? promoCode, string referenceMessage)
   {
       var pricing = await PriceAsync(userId, plan, existing, listPrice, promoCode);
       if (pricing.PromoError != null) throw new ValidationException(pricing.PromoError);
       if (pricing.Total > 0) throw new ValidationException(referenceMessage);
   }

   /// A Pending payment that the promo discount and/or credit fully cover; it completes without Interac or Stripe.
   private static bool IsSettledByAdjustments(Payment payment) =>
       payment.Status == PaymentStatus.Pending && payment.Amount == 0
       && (payment.DiscountAmount > 0 || payment.CreditApplied > 0);

   private async Task<string?> GetAppliedPromoCodeAsync(Payment payment)
   {
       if (payment.PromoCodeId is not Guid promoCodeId) return null;
       return payment.PromoCode?.Code ?? (await _promoCodeRepository.GetByIdAsync(promoCodeId))?.Code;
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

   public Task<int> RunDailyDropInBillingAsync() => RunDropInBillingAsync(DateTime.UtcNow);

   public async Task<int> RunDropInBillingAsync(DateTime nowUtc)
   {
       var todayLocal = SessionTimeHelper.ToLocal(nowUtc).Date;

       // Yesterday is included so a late session whose billing hour lands after midnight isn't missed.
       var candidates = await _sessionRepository.GetSessionsBetweenDatesAsync(todayLocal.AddDays(-1), todayLocal);
       // Open or full: a session that filled up still has drop-in players to bill. Cancelled and completed ones are skipped.
       var todays = new List<Session>();
       foreach (var candidate in candidates.Where(s => s.Status == SessionStatus.Open || s.Status == SessionStatus.Full))
       {
           if (DropInBillingSchedule.ParseStartTime(candidate.StartTime) is null)
           {
               // Surface the bad row instead of billing it at the fallback hour, which for an evening
               // session would be hours before it starts.
               _logger.LogWarning("DropInBilling: session {SessionId} on {SessionDate:yyyy-MM-dd} has an unusable start time '{StartTime}'; not billed",
                   candidate.Id, candidate.SessionDate, candidate.StartTime);
               continue;
           }

           if (DropInBillingSchedule.IsDue(candidate.SessionDate, candidate.StartTime, nowUtc))
               todays.Add(candidate);
       }

       if (todays.Count == 0)
       {
           _logger.LogInformation("DropInBilling: no sessions due for billing; nothing to do");
           return 0;
       }

       // Per-season plan choice changes who is billable: the answer is no longer the player's one
       // global plan but what they chose for the season this session belongs to. Loaded once per run.
       var perSeasonPlans = await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.SeasonPlanChoice);
       var allSeasons = perSeasonPlans ? await _seasonRepository.GetAllAsync() : new List<Season>();

       var created = 0;
       foreach (var session in todays)
       {
           // Resolved ONCE per session, never per registration: this runs hourly, forever, and a
           // lookup inside the inner loop would be a query per player per session.
           Season? sessionSeason = null;
           if (perSeasonPlans)
           {
               sessionSeason = SeasonForDate.Resolve(allSeasons, session.SessionDate);
               if (sessionSeason is null)
               {
                   // Refuse rather than guess. Defaulting either way is silent and costs money:
                   // treat-as-drop-in bills every pass holder through the off-season, and
                   // treat-as-season quietly stops billing drop-ins for weeks.
                   _logger.LogWarning(
                       "DropInBilling: session {SessionId} on {SessionDate:yyyy-MM-dd} not billed — {Reason}",
                       session.Id, session.SessionDate, SeasonForDate.Explain(allSeasons, session.SessionDate));
                   continue;
               }
           }

           var registrations = await _registrationRepository.GetBySessionIdAsync(session.Id);
           if (registrations.Count == 0) continue;

           // Bulk-fetch users in one query instead of N round-trips.
           var userIds = registrations.Select(r => r.UserId).Distinct().ToList();
           var users = (await _userRepository.GetUsersByIdsAsync(userIds)).ToDictionary(u => u.Id);

           foreach (var reg in registrations)
           {
               if (!users.TryGetValue(reg.UserId, out var user)) continue;

               if (perSeasonPlans)
               {
                   // No choice recorded means drop-in — the stated default for everyone, and the
                   // safe direction: an unasked player gets billed rather than playing for free.
                   var choice = await _seasonPlanChoiceRepository.GetAsync(sessionSeason!.Id, reg.UserId);
                   if ((choice?.Plan ?? PaymentPlan.DropIn) != PaymentPlan.DropIn) continue;
               }
               else if (user.PaymentPlan != PaymentPlan.DropIn) continue;

               // Any payment at all — a refunded one included — means this player has already been billed
               // for this session. A session stays billable for up to 48 hours now, so without this the next
               // hourly run would undo an admin's refund with a fresh Pending payment and another email.
               // Only this unattended sweep refuses; the QR check-in path still lets a refunded player pay again.
               if (await _paymentRepository.HasAnyPaymentForSessionAsync(reg.UserId, session.Id)) continue;

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
