using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public required string? Email { get; set; }

    [Required]
    public required string Token { get; set; }

    [Required]
    [MinLength(6)]
    public required string NewPassword { get; set; }
}