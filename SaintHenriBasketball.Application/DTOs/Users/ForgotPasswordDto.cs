using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}