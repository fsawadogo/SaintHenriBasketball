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
using Microsoft.Extensions.Caching.Memory;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.VolunteerRoles;
using SaintHenriBasketball.Application.FeatureFlags;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class UsersController(
    IUserService userService,
    ILogger<UsersController> logger,
    ICacheService cacheService,
    IAccountLifecycleService accountLifecycle,
    IAuditLogService auditLogService,
    IMemoryCache memoryCache,
    IUserDirectoryService userDirectory,
    IStaffRoleService staffRoles,
    ISeasonPlanService seasonPlan)
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
    [AllowTwoFactorEnrollment]
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
            // Players change their plan through PATCH me/payment-plan, and never their own admin access.
            updateUserDto.PaymentPlan = null;
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

            // Taking a season pass goes through the plan service, which counts the spots and claims
            // one under a lock. This endpoint used to assign the plan directly, which is a way round
            // a sold-out season and a way to oversell it.
            if (updatePaymentPlanDto.PaymentPlan == Domain.Enums.PaymentPlan.Season)
            {
                await seasonPlan.ChooseAsync(userId, Domain.Enums.PaymentPlan.Season);
                return NoContent();
            }

            var before = await _userService.GetUserAsync(userId);
            await _userService.UpdateUserPaymentPlanAsync(userId, updatePaymentPlanDto.PaymentPlan);
            _logger.LogInformation("User updated their payment plan successfully: {UserId}", userId);
            if (before.PaymentPlan != updatePaymentPlanDto.PaymentPlan)
                await auditLogService.LogAsync("PlanChangedByPlayer", "User", userId,
                    $"Plan: {before.PaymentPlan} -> {updatePaymentPlanDto.PaymentPlan}", userId, User.AuditUserName());

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
    /// Admin players list: search, filters, engagement and paging run on the server (Admin only)
    /// </summary>
    [HttpGet("directory")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserDirectoryPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDirectoryPageDto>> GetDirectory([FromQuery] UserDirectoryQuery query)
    {
        try
        {
            return Ok(await userDirectory.SearchAsync(query));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
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

            var before = await _userService.GetUserAsync(userId);
            var after = await _userService.UpdateUserAsync(userId, updateUserDto);
            _logger.LogInformation("User updated successfully: {UserId}", userId);
            AuthUserCache.Forget(memoryCache, userId);
            await auditLogService.LogAsync("Updated", "User", userId, DescribeUserChange(before, after), User.AuditUserId(), User.AuditUserName());

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
    /// Deactivate a player (Admin only). Payments and history are kept; <paramref name="anonymize"/> also
    /// erases their personal details.
    /// </summary>
    [HttpDelete("{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId, [FromQuery] bool anonymize = false)
    {
        try
        {
            if (User.AuditUserId() == userId)
                return BadRequest("You can't deactivate your own account from the admin area.");

            var target = await _userService.GetUserAsync(userId);
            if (target.IsAdmin && !target.IsDeactivated && await accountLifecycle.CountActiveAdminsAsync() <= 1)
                return BadRequest("This is the club's last active admin. Make someone else an admin first.");

            await accountLifecycle.DeactivateAsync(userId, anonymize);
            await ForgetUserAsync(userId);
            await auditLogService.LogAsync(anonymize ? "DeactivatedAndAnonymized" : "Deactivated", "User", userId,
                $"{target.FirstName} {target.LastName}".Trim(), User.AuditUserId(), User.AuditUserName());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Let a deactivated player sign in again (Admin only).
    /// </summary>
    [HttpPost("{userId}/reactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateUser(Guid userId)
    {
        try
        {
            await accountLifecycle.ReactivateAsync(userId);
            await ForgetUserAsync(userId);
            await auditLogService.LogAsync("Reactivated", "User", userId, null, User.AuditUserId(), User.AuditUserName());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Mark a player's email confirmed without them following the link (Admin only).
    ///
    /// This vouches for the address on the player's behalf, so it is audited by name. Where the
    /// player can still receive mail, resend the confirmation instead and let them prove it.
    /// </summary>
    [HttpPost("{userId}/confirm-email")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmUserEmail(Guid userId)
    {
        try
        {
            var changed = await accountLifecycle.ConfirmEmailAsync(userId);
            if (!changed) return NoContent();

            await ForgetUserAsync(userId);
            await auditLogService.LogAsync("EmailConfirmedByAdmin", "User", userId, null, User.AuditUserId(), User.AuditUserName());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Send a player a fresh confirmation link (Admin only).</summary>
    [HttpPost("{userId}/resend-confirmation")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendConfirmation(Guid userId)
    {
        try
        {
            var sent = await accountLifecycle.ResendConfirmationAsync(userId);
            if (sent)
                await auditLogService.LogAsync("ConfirmationEmailResent", "User", userId, null, User.AuditUserId(), User.AuditUserName());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Erase my personal details and close my account (Quebec Law 25). Payment records are kept.
    /// </summary>
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMyAccount()
    {
        var userId = User.AuditUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var me = await _userService.GetUserAsync(userId.Value);
            if (me.IsAdmin && await accountLifecycle.CountActiveAdminsAsync() <= 1)
                return BadRequest("You're the club's last active admin. Make someone else an admin before closing your account.");

            await accountLifecycle.DeactivateAsync(userId.Value, anonymize: true);
            await ForgetUserAsync(userId.Value);
            // The audit entry keeps only the account id: the player asked for their details to be erased.
            await auditLogService.LogAsync("AccountClosedByPlayer", "User", userId, null, userId, "Player");
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    private async Task ForgetUserAsync(Guid userId)
    {
        AuthUserCache.Forget(memoryCache, userId);
        await cacheService.RemoveAsync($"Users:Current:{userId}");
        await cacheService.RemoveAsync($"Users:Detail:{userId}");
        await cacheService.RemoveAsync("Users:All");
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
            if (user.PaymentPlan != updatePaymentPlanDto.PaymentPlan)
                await auditLogService.LogAsync("PlanChanged", "User", user.Id,
                    $"Plan: {user.PaymentPlan} -> {updatePaymentPlanDto.PaymentPlan}", User.AuditUserId(), User.AuditUserName());
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

    public record SetAdminRequest(bool IsAdmin);

    /// <summary>
    /// Give or remove admin access (Admin only). You can't remove your own access or the last active admin's.
    /// </summary>
    [HttpPut("{userId}/admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAdmin(Guid userId, [FromBody] SetAdminRequest request)
    {
        try
        {
            var target = await _userService.GetUserAsync(userId);
            if (target.IsAdmin == request.IsAdmin) return NoContent();
            if (request.IsAdmin && target.IsDeactivated)
                return BadRequest("Reactivate this player before giving them admin access.");
            if (!request.IsAdmin)
            {
                if (User.AuditUserId() == userId)
                    return BadRequest("You can't remove your own admin access. Ask another admin.");
                if (await accountLifecycle.CountActiveAdminsAsync() <= 1)
                    return BadRequest("This is the club's last active admin. Make someone else an admin first.");
            }

            await accountLifecycle.SetAdminAsync(userId, request.IsAdmin);
            await ForgetUserAsync(userId);
            await auditLogService.LogAsync(request.IsAdmin ? "AdminGranted" : "AdminRevoked", "User", userId,
                $"{target.FirstName} {target.LastName}".Trim(), User.AuditUserId(), User.AuditUserName());

            if (request.IsAdmin)
            {
                var emailResult = await _userService.SendTargetedEmailsAsync(
                    EmailType.GeneralAnnouncement,
                    new List<string?> { target.Email },
                    target.PreferredLanguage,
                    "You have been given admin access to Saint-Henri Basketball.",
                    "Vous avez maintenant un accès administrateur à Saint-Henri Basketball.");
                if (!emailResult.AllSucceeded)
                    _logger.LogWarning("Failed to send the admin access email to {UserId}", userId);
            }

            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Turn off another admin's two-factor authentication after they lose their device (Admin only). Audited.
    /// </summary>
    [HttpPost("{userId}/reset-2fa")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetTwoFactor(Guid userId)
    {
        if (User.AuditUserId() == userId)
            return BadRequest("You can't reset your own two-factor authentication. Turn it off from your profile with a current code, or ask another admin.");
        try
        {
            await accountLifecycle.ResetTwoFactorAsync(userId);
            await ForgetUserAsync(userId);
            await auditLogService.LogAsync("TwoFactorReset", "User", userId, "Two-factor authentication reset by an admin", User.AuditUserId(), User.AuditUserName());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// Kept for older clients; same rules as PUT {userId}/admin.
    [HttpPatch("{userId}/make-admin")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> MakeUserAdmin(Guid userId) => SetAdmin(userId, new SetAdminRequest(true));

    /// <summary>
    /// Give, change or remove a player's volunteer role: None, CourtCaptain or Treasurer (Admin only). Audited.
    /// Admins (they already have full access) and deactivated players can't get a role; clearing to None is always allowed.
    /// The player's current token stops working, so they sign in again with the new role.
    /// </summary>
    [HttpPut("{userId}/staff-role")]
    [Authorize(Roles = "Admin")]
    [RequireFeature(FeatureFlagKeys.VolunteerRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStaffRole(Guid userId, [FromBody] SetStaffRoleRequest request)
    {
        try
        {
            var change = await staffRoles.SetStaffRoleAsync(userId, request?.Role);
            if (!change.Changed) return NoContent();

            await ForgetUserAsync(userId);
            await auditLogService.LogAsync("StaffRoleChanged", "User", userId,
                $"{change.PlayerName}: staff role {change.Before} -> {change.After}", User.AuditUserId(), User.AuditUserName());
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    private static string DescribeUserChange(UserDto before, UserDto after)
    {
        var changes = new List<string>();
        if (before.Email != after.Email) changes.Add("Email changed");
        if (before.Username != after.Username) changes.Add("Username changed");
        if (before.FirstName != after.FirstName || before.LastName != after.LastName) changes.Add("Name changed");
        if (before.PaymentPlan != after.PaymentPlan) changes.Add($"Plan: {before.PaymentPlan} -> {after.PaymentPlan}");
        return changes.Count == 0 ? "No change" : string.Join("; ", changes);
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
            _logger.LogInformation("Admin sent password reset link to user: {UserId}", userId);
            await auditLogService.LogAsync("PasswordResetSent", "User", userId, null, User.AuditUserId(), User.AuditUserName());

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
    private static string GenerateTemporaryPassword(int length = 24)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+";
        return System.Security.Cryptography.RandomNumberGenerator.GetString(chars, length);
    }

    #endregion
}