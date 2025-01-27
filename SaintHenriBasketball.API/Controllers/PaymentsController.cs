using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Payment;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PaymentsController : ControllerBase
{
   private readonly IPaymentService _paymentService;
   private readonly IEmailService _emailService;
   private readonly IUserService _userService;
   private readonly ILogger<PaymentsController> _logger;

   /// <inheritdoc />
   public PaymentsController(
       IPaymentService paymentService,
       IEmailService emailService,
       IUserService userService, 
       ILogger<PaymentsController> logger)
   {
       _paymentService = paymentService;
       _emailService = emailService;
       _userService = userService;
       _logger = logger;
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
           var user = await _userService.GetUserAsync(payment.UserId);

           return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
       }
       catch (ValidationException ex)
       {
           _logger.LogError(ex.Message);
           return BadRequest(ex.Message);
       }
   }

   [HttpGet("{id}")]
   [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<PaymentDto>> GetPayment(Guid id)
   {
       try
       {
           var payment = await _paymentService.GetPaymentAsync(id);
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
       var payments = await _paymentService.GetUserPaymentsAsync(userId);
       return Ok(payments);
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
           var user = await _userService.GetUserAsync(payment.UserId);

           switch (payment.Status)
           {
               case PaymentStatus.Completed:
                   await _emailService.SendPaymentConfirmationAsync(
                       payment.UserEmail,
                       payment.Amount,
                       payment.Reference,
                       user.PreferredLanguage
                   );
                   break;

               case PaymentStatus.Pending:
                   await _emailService.SendPaymentReminderEmailAsync(
                       payment.UserEmail,
                       user.FirstName + " " + user.LastName,
                       user.PaymentPlan
                   );
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
       var summary = await _paymentService.GetPaymentSummaryAsync();
       return Ok(summary);
   }

   [HttpGet("pending")]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
   public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPendingPayments()
   {
       var payments = await _paymentService.GetPendingPaymentsAsync();
       return Ok(payments);
   }

   [HttpGet]
   [Authorize(Roles = "Admin")]
   [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
   public async Task<ActionResult<IEnumerable<PaymentDto>>> GetAllPayments()
   {
       var payments = await _paymentService.GetAllPayments();
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