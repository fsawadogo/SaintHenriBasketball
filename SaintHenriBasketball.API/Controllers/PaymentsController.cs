using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;

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
   private readonly ILogger<PaymentsController> _logger;
   private readonly ICacheService _cacheService;

    /// <inheritdoc />
    public PaymentsController(
       IPaymentService paymentService,
       IEmailService emailService,
       IUserService userService, 
       ILogger<PaymentsController> logger,
       ICacheService cacheService)
    {
        _paymentService = paymentService;
        _emailService = emailService;
        _userService = userService;
        _logger = logger;
        _cacheService = cacheService;
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
            return Ok(cachedPayment);
        }

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);

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

   [HttpPut("{id}")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(StatusCodes.Status204NoContent)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> UpdatePayment(Guid id, [FromBody] UpdatePaymentDto updatePaymentDto)
   {
       try
       {
           var payment = await _paymentService.UpdatePaymentAsync(id, updatePaymentDto);
           
           // Invalidate all relevant caches
           await _cacheService.RemoveAsync($"Payments:Detail:{id}");
           await _cacheService.RemoveAsync($"Payments:User:{payment.UserId}");
           await _cacheService.RemoveAsync("Payments:All");
           await _cacheService.RemoveAsync("Payments:Pending");
           await _cacheService.RemoveAsync("Payments:Summary");
           
           return NoContent();
       }
       catch (NotFoundException ex)
       {
           _logger.LogError(ex, ex.Message);
           return NotFound(ex.Message);
       }
   }
   
   [HttpPut("{id}/status")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(StatusCodes.Status204NoContent)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] UpdatePaymentStatusDto updateDto)
   {
       try
       {
            var payment = await _paymentService.UpdatePaymentStatusAsync(id, updateDto.Status);

            // Invalidate all relevant caches
            await _cacheService.RemoveAsync($"Payments:Detail:{id}");
            await _cacheService.RemoveAsync($"Payments:User:{payment.UserId}");
            await _cacheService.RemoveAsync("Payments:All");
            await _cacheService.RemoveAsync("Payments:Pending");
            await _cacheService.RemoveAsync("Payments:Summary");
            
            var user = await _userService.GetUserAsync(payment.UserId);

           switch (payment.Status)
           {
               case PaymentStatus.Completed:
                   await _emailService.SendPaymentConfirmationAsync(
                       payment.UserId,
                       payment.Amount,
                       payment.Reference);
                   break;

               case PaymentStatus.Pending:
                   await _emailService.SendPaymentReminderEmailAsync(payment.UserId,user.PaymentPlan);
                   break;

               case PaymentStatus.Failed:
                   var customMessage = $"Your payment of ${payment.Amount} has failed. Please try again or contact support.";
                   await _emailService.SendGeneralAnnouncementEmailAsync(
                       payment.UserEmail,
                       user.FirstName + " " + user.LastName,
                       customMessage
                   );
                   break;
           }

           return NoContent();
       }
       catch (NotFoundException ex)
       {
           _logger.LogError(ex, ex.Message);
           return NotFound(ex.Message);
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
}