using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Application.DTOs.Users;
using System.Security.Claims;
using SaintHenriBasketball.Application.Exceptions;
using ValidationException = SaintHenriBasketball.Application.Exceptions.ValidationException;
using SaintHenriBasketball.Application.Services.Implementations;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Authentication
    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponseDto>> Register([FromBody] RegisterUserDto registerDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!new EmailAddressAttribute().IsValid(registerDto.Email))
                return BadRequest("Invalid email format");

            if (string.IsNullOrWhiteSpace(registerDto.Password) || registerDto.Password.Length < 6)
                return BadRequest("Password must be at least 6 characters long");

            var result = await _userService.RegisterAsync(registerDto);
            _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);

            return CreatedAtAction(nameof(GetCurrentUser), new { email = result.Email }, result);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Registration failed for {Email}: {Message}", registerDto.Email, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for {Email}", registerDto.Email);
            return StatusCode(500, "An unexpected error occurred during registration");
        }
    }

    /// <summary>
    /// Authenticate a user
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrEmpty(loginDto.UserName))
                return BadRequest("Invalid username");

            var result = await _userService.LoginAsync(loginDto);
            _logger.LogInformation("User logged in successfully: {UserName}", loginDto.UserName);

            return Ok(result);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Login failed for {UserName}: {Message}", loginDto.UserName, ex.Message);
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {UserName}", loginDto.UserName);
            return StatusCode(500, "An unexpected error occurred during login");
        }
    }
    #endregion

    #region Current User Operations
    /// <summary>
    /// Get current user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserDto> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        var emailClaim = User.FindFirst(ClaimTypes.Email);
        var usernameClaim = User.FindFirst(ClaimTypes.Name);
        var roleClaim = User.FindFirst(ClaimTypes.Role);

        if (userIdClaim == null || emailClaim == null || usernameClaim == null)
            return Unauthorized("Invalid token claims");

        var userDto = new UserDto
        {
            Id = Guid.Parse(userIdClaim.Value),
            Email = emailClaim.Value,
            Username = usernameClaim.Value,
            IsAdmin = roleClaim?.Value == "Admin"
        };

        return Ok(userDto);
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized("Invalid token claims");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!string.IsNullOrEmpty(updateUserDto.Email) && !new EmailAddressAttribute().IsValid(updateUserDto.Email))
                return BadRequest("Invalid email format");

            await _userService.UpdateUserAsync(Guid.Parse(userIdClaim.Value), updateUserDto);
            _logger.LogInformation("User updated their profile successfully: {UserId}", userIdClaim.Value);

            return NoContent();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("User profile update failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during profile update");
            return StatusCode(500, "An unexpected error occurred during profile update");
        }
    }

    /// <summary>
    /// Update current user's payment plan
    /// </summary>
    [HttpPatch("me/payment-plan")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCurrentUserPaymentPlan([FromBody] UpdatePaymentPlanDto updatePaymentPlanDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized("Invalid token claims");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _userService.UpdateUserPaymentPlanAsync(Guid.Parse(userIdClaim.Value), updatePaymentPlanDto.PaymentPlan);
            _logger.LogInformation("User updated their payment plan successfully: {UserId}", userIdClaim.Value);

            return NoContent();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Payment plan update failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment plan update");
            return StatusCode(500, "An unexpected error occurred while updating payment plan");
        }
    }
    #endregion

    #region Admin Operations
    /// <summary>
    /// Get all users (Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Update user profile (Admin only)
    /// </summary>
    [HttpPut("{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserDto updateUserDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!string.IsNullOrEmpty(updateUserDto.Email) && !new EmailAddressAttribute().IsValid(updateUserDto.Email))
                return BadRequest("Invalid email format");

            await _userService.UpdateUserAsync(userId, updateUserDto);
            _logger.LogInformation("User updated successfully: {UserId}", userId);

            return NoContent();
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("User update failed - user not found: {UserId}", userId);
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("User update failed for {UserId}: {Message}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating user {UserId}", userId);
            return StatusCode(500, "An unexpected error occurred during user update");
        }
    }

    /// <summary>
    /// Delete a user (Admin only)
    /// </summary>
    [HttpDelete("{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        try
        {
            await _userService.DeleteUserAsync(userId);
            _logger.LogInformation("User deleted successfully: {UserId}", userId);

            return NoContent();
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("User deletion failed - user not found: {UserId}", userId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting user {UserId}", userId);
            return StatusCode(500, "An unexpected error occurred during user deletion");
        }
    }
    #endregion

    #region Account Management
    /// <summary>
    /// Confirm user's email
    /// </summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string email)
    {
        try
        {
            await _userService.ConfirmEmailAsync(email, token);
            return Ok("Email confirmed successfully");
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Request password reset
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        try
        {
            await _userService.ForgotPasswordAsync(forgotPasswordDto.Email);
            return Ok("If the email exists, a password reset link has been sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in forgot password for {Email}", forgotPasswordDto.Email);
            return Ok("If the email exists, a password reset link has been sent");
        }
    }

    /// <summary>
    /// Reset password
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        try
        {
            await _userService.ResetPasswordAsync(resetPasswordDto);
            return Ok("Password has been reset successfully");
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Send targeted emails to specific users (Admin only)
    /// </summary>
    [HttpPost("send-email")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendTargetedEmails([FromBody] SendEmailRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!request.HasValidEmails())
            {
                return BadRequest("One or more email addresses are invalid");
            }

            var result = await _userService.SendTargetedEmailsAsync(
                request.EmailType,
                request.Emails,
                request.Language,
                request.CustomMessage,
                request.CustomMessageFr);

            if (result.AllSucceeded)
            {
                _logger.LogInformation(
                    "Successfully sent {EmailType} emails to {Count} recipients in {Language}",
                    request.EmailType, result.SuccessCount, request.Language);

                return Ok(new
                {
                    Message = $"Successfully sent emails to {result.SuccessCount} recipients",
                    result.SuccessCount
                });
            }

            _logger.LogWarning(
                "Partially completed sending {EmailType} emails. Success: {SuccessCount}, Failed: {FailureCount}",
                request.EmailType, result.SuccessCount, result.FailureCount);

            return Ok(new
            {
                result.SuccessCount,
                result.FailureCount,
                result.FailedEmails
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending targeted emails of type {EmailType}", request.EmailType);
            return StatusCode(500, "An unexpected error occurred while sending emails");
        }
    }
    #endregion
}