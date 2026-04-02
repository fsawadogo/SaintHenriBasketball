using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Application.DTOs.Users;
using System.Security.Claims;
using SaintHenriBasketball.Application.Exceptions;
using ValidationException = SaintHenriBasketball.Application.Exceptions.ValidationException;
using SaintHenriBasketball.Domain.Enums;
using Microsoft.AspNetCore.RateLimiting;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class UsersController(IUserService userService, ILogger<UsersController> logger, ICacheService cacheService)
    : ControllerBase
{
    private readonly IUserService _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    private readonly ILogger<UsersController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    #region Authentication
    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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

    [HttpPost("google-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponseDto>> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        try
        {
            var result = await _userService.GoogleLoginAsync(dto.AccessToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google login failed");
            return StatusCode(500, "An unexpected error occurred during Google login");
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
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized("Invalid token claims");

        var userId = Guid.Parse(userIdClaim.Value);

        // Check cache first
        string cacheKey = $"Users:Current:{userId}";
        var cachedUser = await cacheService.GetAsync<UserDto>(cacheKey);

        if (cachedUser != null)
        {
            _logger.LogInformation("Current user retrieved from cache for user {UserId}", userId);
            return Ok(cachedUser);
        }

        // Get from service if not in cache
        var userDto = await _userService.GetUserAsync(userId);

        // Cache for 15 minutes
        await cacheService.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(15));

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
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized("Invalid token claims");

        var userId = Guid.Parse(userIdClaim.Value);

        try
        {
            await _userService.UpdateUserAsync(userId, updateUserDto);

            // Invalidate cache
            await cacheService.RemoveAsync($"Users:Current:{userId}");
            await cacheService.RemoveAsync($"Users:Detail:{userId}");
            await cacheService.RemoveAsync("Users:All");

            return NoContent();
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
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

            var userId = Guid.Parse(userIdClaim.Value);
            await _userService.UpdateUserPaymentPlanAsync(userId, updatePaymentPlanDto.PaymentPlan);
            _logger.LogInformation("User updated their payment plan successfully: {UserId}", userId);

            // Invalidate cache
            await cacheService.RemoveAsync($"Users:Current:{userId}");
            await cacheService.RemoveAsync($"Users:Detail:{userId}");
            await cacheService.RemoveAsync("Users:All");

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

            // Invalidate cache
            await cacheService.RemoveAsync($"Users:Current:{userId}");
            await cacheService.RemoveAsync($"Users:Detail:{userId}");
            await cacheService.RemoveAsync("Users:All");

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

            // Invalidate cache
            await cacheService.RemoveAsync($"Users:Current:{userId}");
            await cacheService.RemoveAsync($"Users:Detail:{userId}");
            await cacheService.RemoveAsync("Users:All");

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
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string? email)
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

    [HttpPatch("update-payment-plan")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateUserPaymentPlanByEmail([FromQuery] string? email, [FromBody] UpdatePaymentPlanDto updatePaymentPlanDto)
    {
        try
        {
            if (string.IsNullOrEmpty(email) || !new EmailAddressAttribute().IsValid(email))
                return BadRequest("Invalid email format");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userService.GetUserByEmailAsync(email);

            await _userService.UpdateUserPaymentPlanAsync(user.Id, updatePaymentPlanDto.PaymentPlan);
            _logger.LogInformation("User payment plan updated successfully: {Email}", email);

            // Invalidate cache
            await cacheService.RemoveAsync($"Users:Current:{user.Id}");
            await cacheService.RemoveAsync($"Users:Detail:{user.Id}");
            await cacheService.RemoveAsync("Users:All");

            return NoContent();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Payment plan update failed for {Email}: {Message}", email, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment plan update for {Email}", email);
            return StatusCode(500, "An unexpected error occurred while updating payment plan");
        }
    }

    [HttpPatch("{userId}/make-admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MakeUserAdmin(Guid userId)
    {
        try
        {
            var user = await _userService.GetUserAsync(userId);

            var updateUserDto = new UpdateUserDto
            {
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PaymentPlan = user.PaymentPlan,
                IsAdmin = true
            };

            await _userService.UpdateUserAsync(userId, updateUserDto);
            _logger.LogInformation("User made admin successfully: {UserId}", userId);

            // Invalidate cache
            await cacheService.RemoveAsync($"Users:Current:{userId}");
            await cacheService.RemoveAsync($"Users:Detail:{userId}");
            await cacheService.RemoveAsync("Users:All");

            // Send email notification
            var emailResult = await _userService.SendTargetedEmailsAsync(
                EmailType.GeneralAnnouncement,
                new List<string?> { user.Email },
                EmailLanguage.English,
                "You have been granted admin privileges."
            );

            if (!emailResult.AllSucceeded)
            {
                _logger.LogWarning("Failed to send admin notification email to {Email}", user.Email);
            }

            return Ok("User made admin successfully");
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Make admin failed for {UserId}: {Message}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error making user admin {UserId}", userId);
            return StatusCode(500, "An unexpected error occurred while making user admin");
        }
    }

    /// <summary>
    /// Create a new user with temporary password and send password reset email (Admin only)
    /// </summary>
    [HttpPost("create-user")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] RegisterUserDto createUserDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!new EmailAddressAttribute().IsValid(createUserDto.Email))
                return BadRequest("Invalid email format");

            // Generate a secure temporary password
            string temporaryPassword = GenerateTemporaryPassword();

            var registerDto = new RegisterUserDto
            {
                Username = createUserDto.Username,
                Email = createUserDto.Email,
                Password = temporaryPassword,
                FirstName = createUserDto.FirstName,
                LastName = createUserDto.LastName
            };

            // Register the user with temporary password
            var result = await _userService.RegisterAsync(registerDto);

            _logger.LogInformation("User created by admin successfully: {Email}", createUserDto.Email);

            // Invalidate cache
            await cacheService.RemoveAsync("Users:All");
            // Note: User-specific caches don't need invalidation for new users

            // Immediately send password reset email to the user
            await _userService.ForgotPasswordAsync(createUserDto.Email);

            _logger.LogInformation("Password reset email sent to newly created user: {Email}", createUserDto.Email);

            // Send email notification about account creation
            var emailResult = await _userService.SendTargetedEmailsAsync(
                EmailType.AccountCreated,
                new List<string?> { createUserDto.Email },
                EmailLanguage.English,
                "Your account has been created by an administrator. Please check your email to set your password."
            );

            if (!emailResult.AllSucceeded)
            {
                _logger.LogWarning("Failed to send account creation notification email to {Email}", createUserDto.Email);
            }

            return CreatedAtAction(nameof(GetCurrentUser), new { email = result.Email }, result);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("User creation failed for {Email}: {Message}", createUserDto.Email, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during user creation for {Email}", createUserDto.Email);
            return StatusCode(500, "An unexpected error occurred during user creation");
        }
    }

    /// <summary>
    /// Send a password reset link to a specific user (Admin only)
    /// </summary>
    [HttpPost("{userId}/send-password-reset")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendPasswordResetLink(Guid userId)
    {
        try
        {
            var user = await _userService.GetUserAsync(userId);

            if (user == null)
                return NotFound($"User with ID {userId} not found");

            await _userService.ForgotPasswordAsync(user.Email);
            _logger.LogInformation("Admin sent password reset link to user: {UserId}, {Email}", userId, user.Email);

            return Ok($"Password reset link has been sent to {user.Email}");
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Send password reset failed - user not found: {UserId}", userId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending password reset for user {UserId}", userId);
            return StatusCode(500, "An unexpected error occurred while sending password reset");
        }
    }

    /// <summary>
    /// Generates a secure temporary password
    /// </summary>
    private string GenerateTemporaryPassword(int length = 12)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+";
        var random = new Random();

        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    #endregion
}