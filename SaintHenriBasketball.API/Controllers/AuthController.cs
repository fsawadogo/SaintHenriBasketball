using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Application.DTOs.Auth;
using System.Security.Claims;
using ValidationException = SaintHenriBasketball.Application.Exceptions.ValidationException;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
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
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterUserDto registerDto)
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
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate email format
            if (!new EmailAddressAttribute().IsValid(loginDto.Email))
            {
                return BadRequest("Invalid email format");
            }

            var result = await _authService.LoginAsync(loginDto);

            _logger.LogInformation("User logged in successfully: {Email}", loginDto.Email);

            return Ok(result);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Login failed for {Email}: {Message}", loginDto.Email, ex.Message);
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Email}", loginDto.Email);
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
}