using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class PaymentsController : ControllerBase
{
   private readonly IPaymentService _paymentService;
   private readonly IEmailService _emailService;
   private readonly IUserService _userService;
   private readonly IStripeService _stripeService;
   private readonly IAuditLogService _auditLogService;
   private readonly ILogger<PaymentsController> _logger;
   private readonly ICacheService _cacheService;
   private readonly IWebHostEnvironment _webHostEnvironment;
   private readonly IPaymentRefundService _refundService;

    public PaymentsController(
       IPaymentService paymentService,
       IEmailService emailService,
       IUserService userService,
       IStripeService stripeService,
       IAuditLogService auditLogService,
       ILogger<PaymentsController> logger,
       ICacheService cacheService,
       IWebHostEnvironment webHostEnvironment,
       IPaymentRefundService refundService)
    {
        _paymentService = paymentService;
        _emailService = emailService;
        _userService = userService;
        _stripeService = stripeService;
        _auditLogService = auditLogService;
        _logger = logger;
        _cacheService = cacheService;
        _webHostEnvironment = webHostEnvironment;
        _refundService = refundService;
    }

    [HttpPost]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentDto createPaymentDto)
   {
        try
        {
            var payment = await _paymentService.CreatePaymentAsync(createPaymentDto);

            // Invalidate relevant caches
            await _cacheService.RemoveAsync("Payments:All");
            await _cacheService.RemoveAsync("Payments:Pending");
            await _cacheService.RemoveAsync("Payments:Summary");
            await _cacheService.RemoveAsync($"Payments:User:{payment.UserId}");

            Guid? adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedAdminId) ? parsedAdminId : null;
            var adminName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
            await _auditLogService.LogAsync("Created", "Payment", payment.Id,
                $"Amount: ${payment.Amount}, Plan: {payment.Plan}", adminId, adminName);

            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

   [HttpGet("{id}")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<PaymentDto>> GetPayment(Guid id)
   {
        // Check cache first
        string cacheKey = $"Payments:Detail:{id}";
        var cachedPayment = await _cacheService.GetAsync<PaymentDto>(cacheKey);

        if (cachedPayment != null)
        {
            _logger.LogInformation("Payment {PaymentId} retrieved from cache", id);
            if (cachedPayment.UserId.ToString() != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin")) return Forbid();
            return Ok(cachedPayment);
        }

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);

            if (payment.UserId.ToString() != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin")) return Forbid();
            await _cacheService.SetAsync(cacheKey, payment, TimeSpan.FromMinutes(20));

            return Ok(payment);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

   [HttpGet("user/{userId}")]
   [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
   public async Task<ActionResult<IEnumerable<PaymentDto>>> GetUserPayments(Guid userId)
   {
        if (userId.ToString() != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin")) return Forbid();
        // Check cache first
        string cacheKey = $"Payments:User:{userId}";
        var cachedPayments = await _cacheService.GetAsync<IEnumerable<PaymentDto>>(cacheKey);

        if (cachedPayments != null)
        {
            _logger.LogInformation("User payments for {UserId} retrieved from cache", userId);
            return Ok(cachedPayments);
        }

        var payments = await _paymentService.GetUserPaymentsAsync(userId);

        // Cache for 10 minutes
        await _cacheService.SetAsync(cacheKey, payments, TimeSpan.FromMinutes(10));

        return Ok(payments);
    }

   [HttpGet("history")]
   [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
   public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPaymentHistory()
   {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var payments = await _paymentService.GetUserPaymentsAsync(Guid.Parse(userId));
        return Ok(payments);
   }

   [HttpGet("{id}/invoice")]
   [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status403Forbidden)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> DownloadInvoice(Guid id)
   {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);

            // Verify ownership (unless admin)
            if (payment.UserId != Guid.Parse(userId) && !User.IsInRole("Admin"))
                return Forbid();

            var user = await _userService.GetUserAsync(payment.UserId);
            var billGenerator = new BillPdfGenerator(_webHostEnvironment);

            var billDetails = new BillDetails
            {
                Name = $"{user.FirstName} {user.LastName}",
                Email = user.Email,
                Description = payment.Plan == PaymentPlan.Season ? "Forfait de saison" : "Forfait à la séance",
                Amount = payment.Amount,
                OriginalAmount = payment.OriginalAmount,
                DiscountAmount = payment.DiscountAmount,
                CreditApplied = payment.CreditApplied,
                Reference = payment.Reference,
                Date = payment.PaymentDate,
                // Only a completed payment is a receipt; anything else is still a bill.
                PaidOn = payment.Status == PaymentStatus.Completed ? payment.PaymentDate : null
            };

            var pdfContent = billGenerator.GenerateBill(billDetails);
            return File(pdfContent, "application/pdf", $"invoice_{payment.Reference ?? id.ToString()}.pdf");
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
   }

   [HttpPut("{id}")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(StatusCodes.Status204NoContent)]
   [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> UpdatePayment(Guid id, [FromBody] UpdatePaymentDto updatePaymentDto)
   {
       try
       {
           var before = await _paymentService.GetPaymentAsync(id);
           var payment = await _paymentService.UpdatePaymentAsync(id, updatePaymentDto);
           await InvalidatePaymentCachesAsync(id, payment.UserId);
           await _auditLogService.LogAsync("Updated", "Payment", id, DescribeChange(before, payment), User.AuditUserId(), User.AuditUserName());
           return NoContent();
       }
       catch (ValidationException ex) { return BadRequest(ex.Message); }
       catch (NotFoundException ex) { return NotFound(ex.Message); }
   }

   [HttpPut("{id}/status")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(StatusCodes.Status204NoContent)]
   [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] UpdatePaymentStatusDto updateDto)
   {
       try
       {
           var before = await _paymentService.GetPaymentAsync(id);
           var payment = await _paymentService.UpdatePaymentStatusAsync(id, updateDto.Status);
           await InvalidatePaymentCachesAsync(id, payment.UserId);
           await _auditLogService.LogAsync("StatusChanged", "Payment", id, DescribeChange(before, payment), User.AuditUserId(), User.AuditUserName());
           return NoContent();
       }
       catch (ValidationException ex) { return BadRequest(ex.Message); }
       catch (NotFoundException ex) { return NotFound(ex.Message); }
   }

   /// <summary>
   /// Give a completed payment's money back: to the card (Stripe), as account credit, or recorded as sent
   /// back by hand. Requires a reason; audited; the player is notified.
   /// </summary>
   [HttpPost("{id}/refund")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
   [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<PaymentDto>> RefundPayment(Guid id, [FromBody] RefundPaymentDto request)
   {
       try
       {
           var wasRefunded = (await _paymentService.GetPaymentAsync(id)).Status == PaymentStatus.Refunded;
           var payment = await _refundService.RefundAsync(id, request);
           await InvalidatePaymentCachesAsync(id, payment.UserId);
           if (!wasRefunded)
               await _auditLogService.LogAsync("Refunded", "Payment", id,
                   $"Method: {request.Method}; Amount: {payment.Amount:0.00}; Reason: {request.Reason?.Trim()}",
                   User.AuditUserId(), User.AuditUserName());
           return Ok(payment);
       }
       catch (ValidationException ex) { return BadRequest(ex.Message); }
       catch (NotFoundException ex) { return NotFound(ex.Message); }
       catch (Stripe.StripeException ex)
       {
           _logger.LogError(ex, "Stripe refund failed for payment {PaymentId}", id);
           return BadRequest($"Stripe couldn't refund this card payment: {ex.StripeError?.Message ?? ex.Message}");
       }
   }

   private async Task InvalidatePaymentCachesAsync(Guid paymentId, Guid userId)
   {
       await _cacheService.RemoveAsync($"Payments:Detail:{paymentId}");
       await _cacheService.RemoveAsync($"Payments:User:{userId}");
       await _cacheService.RemoveAsync("Payments:All");
       await _cacheService.RemoveAsync("Payments:Pending");
       await _cacheService.RemoveAsync("Payments:Summary");
   }

   private static string DescribeChange(PaymentDto before, PaymentDto after)
   {
       var changes = new List<string>();
       if (before.Status != after.Status) changes.Add($"Status: {before.Status} -> {after.Status}");
       if (before.Amount != after.Amount) changes.Add($"Amount: {before.Amount:0.00} -> {after.Amount:0.00}");
       if (before.Plan != after.Plan) changes.Add($"Plan: {before.Plan} -> {after.Plan}");
       if (before.SeasonId != after.SeasonId) changes.Add("Season changed");
       return changes.Count == 0 ? "No change" : string.Join("; ", changes);
   }

   /// <summary>
   /// Admin payments list, filtered and paged on the server, with totals for every matching payment
   /// </summary>
   [HttpGet("search")]
   [Authorize(Policy = StaffAccess.TreasurerOrAdminPolicy)]
   [ProducesResponseType(typeof(PaymentSearchResultDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   public async Task<ActionResult<PaymentSearchResultDto>> SearchPayments(
       [FromQuery] string? search,
       [FromQuery] PaymentStatus? status,
       [FromQuery] PaymentPlan? plan,
       [FromQuery] Guid? seasonId,
       [FromQuery] DateTimeOffset? from,
       [FromQuery] DateTimeOffset? to,
       [FromQuery] int page = 1,
       [FromQuery] int pageSize = 50)
   {
       try
       {
           return Ok(await _paymentService.SearchPaymentsAsync(new PaymentSearchCriteria(
               search, status, plan, seasonId, from?.UtcDateTime, to?.UtcDateTime, page, pageSize)));
       }
       catch (ValidationException ex)
       {
           return BadRequest(ex.Message);
       }
   }

   [HttpGet("summary")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(PaymentSummaryDto), StatusCodes.Status200OK)]
   public async Task<ActionResult<PaymentSummaryDto>> GetPaymentSummary()
   {
        // Check cache first
        string cacheKey = "Payments:Summary";
        var cachedSummary = await _cacheService.GetAsync<PaymentSummaryDto>(cacheKey);

        if (cachedSummary != null)
        {
            _logger.LogInformation("Payment summary retrieved from cache");
            return Ok(cachedSummary);
        }

        var summary = await _paymentService.GetPaymentSummaryAsync();

        // Cache for 30 minutes - summary data changes less frequently
        await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(30));

        return Ok(summary);
    }

   [HttpGet("pending")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
   public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPendingPayments()
   {
        // Check cache first, but with a shorter duration since this is more time-sensitive
        string cacheKey = "Payments:Pending";
        var cachedPayments = await _cacheService.GetAsync<IEnumerable<PaymentDto>>(cacheKey);

        if (cachedPayments != null)
        {
            _logger.LogInformation("Pending payments retrieved from cache");
            return Ok(cachedPayments);
        }

        var payments = await _paymentService.GetPendingPaymentsAsync();

        // Cache for only 5 minutes since pending status might change frequently
        await _cacheService.SetAsync(cacheKey, payments, TimeSpan.FromMinutes(5));

        return Ok(payments);
    }

   [HttpGet]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
   public async Task<ActionResult<IEnumerable<PaymentDto>>> GetAllPayments()
   {
        // Check cache first
        string cacheKey = "Payments:All";
        var cachedPayments = await _cacheService.GetAsync<IEnumerable<PaymentDto>>(cacheKey);

        if (cachedPayments != null)
        {
            _logger.LogInformation("All payments retrieved from cache");
            return Ok(cachedPayments);
        }

        var payments = await _paymentService.GetAllPayments();

        // Cache for 10 minutes
        await _cacheService.SetAsync(cacheKey, payments, TimeSpan.FromMinutes(10));

        return Ok(payments);
    }

   [HttpPost("reminder")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(EmailSendResult), StatusCodes.Status200OK)]
   public async Task<IActionResult> SendPaymentReminders()
   {
       var pendingPayments = await _paymentService.GetPendingPaymentsAsync();
       var emails = pendingPayments.Select(p => p.UserEmail).ToList();

       var result = await _emailService.SendPaymentRemindersAsync(
           emails,
           EmailLanguage.English
       );

       return Ok(new { 
           Succeeded = result.SuccessCount,
           Failed = result.FailureCount,
           FailedEmails = result.FailedEmails
       });
   }

   [HttpPost("bulk-reminder")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(EmailSendResult), StatusCodes.Status200OK)]
   public async Task<IActionResult> SendBulkPaymentReminders([FromBody] BulkEmailDto request)
   {
       var result = await _emailService.SendPaymentRemindersAsync(
           request.Emails,
           request.Language,
           request.CustomMessage,
           request.CustomMessageFr
       );

       return Ok(result);
   }

   [HttpGet("reconciliation")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(StatusCodes.Status200OK)]
   public async Task<IActionResult> GetReconciliation([FromQuery] DateTime from, [FromQuery] DateTime to)
   {
       var result = await _paymentService.ReconcilePaymentsAsync(from, to);
       return Ok(result);
   }

   /// <summary>
   /// Create a drop-in payment for a session (user-facing)
   /// </summary>
   [HttpPost("drop-in")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<PaymentDto>> CreateDropInPayment([FromBody] CreateDropInPaymentDto request)
   {
       try
       {
           var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (string.IsNullOrEmpty(userId))
               return Unauthorized();

           var payment = await _paymentService.CreateDropInPaymentAsync(Guid.Parse(userId), request);

           // Invalidate caches
           await _cacheService.RemoveAsync($"Payments:Detail:{payment.Id}");
           await _cacheService.RemoveAsync("Payments:All");
           await _cacheService.RemoveAsync("Payments:Pending");
           await _cacheService.RemoveAsync("Payments:Summary");
           await _cacheService.RemoveAsync($"Payments:User:{userId}");

           return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
       }
       catch (ValidationException ex)
       {
           return BadRequest(ex.Message);
       }
       catch (NotFoundException ex)
       {
           return NotFound(ex.Message);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Error creating drop-in payment");
           return StatusCode(500, "An unexpected error occurred while creating the payment");
       }
   }

   [HttpPost("season")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   public async Task<ActionResult<PaymentDto>> CreateSeasonPayment([FromBody] CreateSeasonPaymentDto request)
   {
       try
       {
           var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
           if (string.IsNullOrEmpty(userId)) return Unauthorized();
           var payment = await _paymentService.CreateSeasonPaymentAsync(Guid.Parse(userId), request);
           await _cacheService.RemoveAsync($"Payments:Detail:{payment.Id}");
           await _cacheService.RemoveAsync("Payments:All");
           await _cacheService.RemoveAsync("Payments:Pending");
           await _cacheService.RemoveAsync("Payments:Summary");
           await _cacheService.RemoveAsync($"Payments:User:{userId}");
           return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
       }
       catch (ValidationException ex) { return BadRequest(ex.Message); }
       catch (NotFoundException ex) { return NotFound(ex.Message); }
   }

   /// <summary>
   /// Price breakdown (promo discount, account credit, total) for the caller's drop-in or season payment. Read-only.
   /// </summary>
   [HttpPost("quote")]
   [ProducesResponseType(typeof(PaymentQuoteDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<PaymentQuoteDto>> GetQuote([FromBody] PaymentQuoteRequestDto request)
   {
       var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
       if (string.IsNullOrEmpty(userId)) return Unauthorized();
       try
       {
           return Ok(await _paymentService.GetQuoteAsync(Guid.Parse(userId), request));
       }
       catch (ValidationException ex) { return BadRequest(ex.Message); }
       catch (NotFoundException ex) { return NotFound(ex.Message); }
   }

   [HttpGet("options")]
   public IActionResult GetOptions([FromServices] IConfiguration configuration)
   {
       var key = configuration["Stripe:SecretKey"];
       return Ok(new {
           interacEmail = configuration["Payments:InteracEmail"] ?? "pay@sainthenribasketball.com",
           cardEnabled = !string.IsNullOrWhiteSpace(key) && (key.StartsWith("sk_") || key.StartsWith("rk_")),
           testMode = configuration.GetValue<bool>("LocalTesting:SuppressEmail"),
           interacMode = "manual",
           interacAutodeposit = configuration.GetValue("Payments:InteracAutodeposit", true),
           interacReceivingBank = "Tangerine",
           interacRecipientName = configuration["Payments:InteracRecipientName"]
       });
   }

   [HttpPost("drop-in/checkout")]
   [ProducesResponseType(StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateDropInPaymentDto request)
   {
       try
       {
           var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (string.IsNullOrEmpty(userId))
               return Unauthorized();

           request.PaymentMethod = 1;
           request.InteracReference = null;
           // Create pending payment record first (applies any promo code and account credit)
           var payment = await _paymentService.CreateDropInPaymentAsync(Guid.Parse(userId), request);
           await _cacheService.RemoveAsync($"Payments:Detail:{payment.Id}");

           // Fully covered by the discount and credit: already completed, nothing to charge.
           if (payment.Status == PaymentStatus.Completed)
           {
               await _cacheService.RemoveAsync("Payments:All");
               await _cacheService.RemoveAsync("Payments:Pending");
               await _cacheService.RemoveAsync("Payments:Summary");
               await _cacheService.RemoveAsync($"Payments:User:{userId}");
               return Ok(new { checkoutUrl = (string?)null, completed = true });
           }

           // Create Stripe Checkout Session
           var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
               Guid.Parse(userId), request.SessionId, payment.Id);

           // Invalidate caches
           await _cacheService.RemoveAsync("Payments:All");
           await _cacheService.RemoveAsync("Payments:Pending");
           await _cacheService.RemoveAsync($"Payments:User:{userId}");

           return Ok(new { checkoutUrl, completed = false });
       }
       catch (ValidationException ex)
       {
           return BadRequest(ex.Message);
       }
       catch (NotFoundException ex)
       {
           return NotFound(ex.Message);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Error creating Stripe checkout session");
           return StatusCode(500, "An unexpected error occurred while creating the checkout session");
       }
   }

   /// <summary>
   /// Start a Stripe Checkout for the caller's season fee (applies any promo code and account credit first)
   /// </summary>
   [HttpPost("season/checkout")]
   [RequireFeature(FeatureFlagKeys.SeasonCardPayments)]
   [ProducesResponseType(StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> CreateSeasonCheckoutSession([FromBody] CreateSeasonPaymentDto request)
   {
       var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
       if (string.IsNullOrEmpty(userId)) return Unauthorized();
       try
       {
           request.PaymentMethod = 1;
           request.InteracReference = null;
           var payment = await _paymentService.CreateSeasonPaymentAsync(Guid.Parse(userId), request);
           await _cacheService.RemoveAsync($"Payments:Detail:{payment.Id}");
           await _cacheService.RemoveAsync("Payments:All");
           await _cacheService.RemoveAsync("Payments:Pending");
           await _cacheService.RemoveAsync("Payments:Summary");
           await _cacheService.RemoveAsync($"Payments:User:{userId}");

           // Fully covered by the discount and credit: already completed, nothing to charge.
           if (payment.Status == PaymentStatus.Completed)
               return Ok(new { checkoutUrl = (string?)null, completed = true });

           var checkoutUrl = await _stripeService.CreateSeasonCheckoutSessionAsync(Guid.Parse(userId), request.SeasonId, payment.Id);
           await _cacheService.RemoveAsync($"Payments:Detail:{payment.Id}");
           return Ok(new { checkoutUrl, completed = false });
       }
       catch (ValidationException ex) { return BadRequest(ex.Message); }
       catch (NotFoundException ex) { return NotFound(ex.Message); }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Error creating Stripe season checkout session");
           return StatusCode(500, "An unexpected error occurred while creating the checkout session");
       }
   }

   /// <summary>
   /// Get payment link info for a drop-in session
   /// </summary>
   [HttpGet("drop-in/link/{sessionId}")]
   [ProducesResponseType(typeof(DropInPaymentLinkDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<DropInPaymentLinkDto>> GetDropInPaymentLink(Guid sessionId)
   {
       try
       {
           var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
           if (string.IsNullOrEmpty(userId))
               return Unauthorized();

           var link = await _paymentService.GetDropInPaymentLinkAsync(Guid.Parse(userId), sessionId);
           return Ok(link);
       }
       catch (NotFoundException ex)
       {
           return NotFound(ex.Message);
       }
   }

   /// <summary>
   /// Confirm an Interac e-Transfer payment with a reference number
   /// </summary>
   [HttpPost("{id}/confirm-interac")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status400BadRequest)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<PaymentDto>> ConfirmInteracPayment(Guid id, [FromBody] ConfirmInteracPaymentDto request)
   {
       try
       {
           var existing = await _paymentService.GetPaymentAsync(id);
           if (existing.UserId.ToString() != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin"))
               return Forbid();
           var payment = await _paymentService.ConfirmInteracPaymentAsync(id, request.Reference);

           // Invalidate caches
           await _cacheService.RemoveAsync($"Payments:Detail:{id}");
           await _cacheService.RemoveAsync($"Payments:User:{payment.UserId}");
           await _cacheService.RemoveAsync("Payments:Pending");

           return Ok(payment);
       }
       catch (ValidationException ex)
       {
           return BadRequest(ex.Message);
       }
       catch (NotFoundException ex)
       {
           return NotFound(ex.Message);
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Error confirming Interac payment {PaymentId}", id);
           return StatusCode(500, "An unexpected error occurred while confirming the payment");
       }
   }

   [HttpPost("batch-invoice")]
   [Authorize(Roles = "Admin")]
   public async Task<IActionResult> SendBatchInvoices([FromBody] BatchInvoiceRequestDto request)
   {
       var payments = (await _paymentService.GetAllPayments())
           .Where(p => p.Status == PaymentStatus.Completed
               && p.PaymentDate >= request.From
               && p.PaymentDate <= request.To)
           .ToList();

       var sent = 0;
       var failed = 0;

       foreach (var payment in payments)
       {
           try
           {
               await _emailService.SendPaymentConfirmationAsync(
                   payment.UserId, payment.Amount, payment.Reference);
               sent++;
           }
           catch
           {
               failed++;
           }
       }

       return Ok(new { sent, failed, total = payments.Count });
   }
}
