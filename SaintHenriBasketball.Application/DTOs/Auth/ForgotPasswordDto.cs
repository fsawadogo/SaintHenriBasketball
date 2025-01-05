using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.Auth;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}