using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Application.DTOs.Users;
using System.Security.Claims;
using SaintHenriBasketball.Application.Exceptions;
using ValidationException = SaintHenriBasketball.Application.Exceptions.ValidationException;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService _authService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService authService, ILogger<UsersController> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <param name="registerDto">The registration details</param>
    /// <returns>Authentication result including JWT token</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponseDto>> Register([FromBody] RegisterUserDto registerDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate email format
            if (!new EmailAddressAttribute().IsValid(registerDto.Email))
            {
                return BadRequest("Invalid email format");
            }

            // Validate password strength
            if (string.IsNullOrWhiteSpace(registerDto.Password) || registerDto.Password.Length < 6)
            {
                return BadRequest("Password must be at least 6 characters long");
            }

            var result = await _authService.RegisterAsync(registerDto);

            _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);

            return CreatedAtAction(nameof(Register), new { email = result.Email }, result);
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
    /// <param name="loginDto">The login credentials</param>
    /// <returns>Authentication result including JWT token</returns>
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
            {
                return BadRequest(ModelState);
            }

            // Validate email format
            if (string.IsNullOrEmpty(loginDto.UserName))
            {
                return BadRequest("Invalid username");
            }

            var result = await _authService.LoginAsync(loginDto);

            _logger.LogInformation("User logged in successfully: {UserName}", loginDto.UserName);

            return Ok(result);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Login failed for {Email}: {Message}", loginDto.UserName, ex.Message);
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Email}", loginDto.UserName);
            return StatusCode(500, "An unexpected error occurred during login");
        }
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    /// <returns>Current user details</returns>
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
        {
            return Unauthorized("Invalid token claims");
        }

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
        /// Get all users
        /// </summary>
        /// <returns>List of all users</returns>
        [HttpGet("users")]
        // [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        {
            var users = await _authService.GetAllUsersAsync();
            return Ok(users);
        }
     
     /// <summary>
/// Update user profile
/// </summary>
/// <param name="userId">The ID of the user to update</param>
/// <param name="updateUserDto">The updated user details</param>
[HttpPut("users/{userId}")]
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
        {
            return BadRequest(ModelState);
        }

        if (!string.IsNullOrEmpty(updateUserDto.Email) && !new EmailAddressAttribute().IsValid(updateUserDto.Email))
        {
            return BadRequest("Invalid email format");
        }

        await _authService.UpdateUserAsync(userId, updateUserDto);
        
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
/// Update current user's profile
/// </summary>
/// <param name="updateUserDto">The updated user details</param>
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
        {
            return Unauthorized("Invalid token claims");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!string.IsNullOrEmpty(updateUserDto.Email) && !new EmailAddressAttribute().IsValid(updateUserDto.Email))
        {
            return BadRequest("Invalid email format");
        }

        await _authService.UpdateUserAsync(Guid.Parse(userIdClaim.Value), updateUserDto);
        
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
/// Delete a user
/// </summary>
/// <param name="userId">The ID of the user to delete</param>
[HttpDelete("users/{userId}")]
[Authorize(Roles = "Admin")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteUser(Guid userId)
{
    try
    {
        await _authService.DeleteUserAsync(userId);
        
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

[HttpPost("confirm-email")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string email)
{
    try
    {
        await _authService.ConfirmEmailAsync(email, token);
        return Ok("Email confirmed successfully");
    }
    catch (ValidationException ex)
    {
        return BadRequest(ex.Message);
    }
}

[HttpPost("forgot-password")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
{
    try
    {
        await _authService.ForgotPasswordAsync(forgotPasswordDto.Email);
        return Ok("If the email exists, a password reset link has been sent");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in forgot password for {Email}", forgotPasswordDto.Email);
        return Ok("If the email exists, a password reset link has been sent");
    }
}

[HttpPost("reset-password")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
{
    try
    {
        await _authService.ResetPasswordAsync(resetPasswordDto);
        return Ok("Password has been reset successfully");
    }
    catch (ValidationException ex)
    {
        return BadRequest(ex.Message);
    }
}
}