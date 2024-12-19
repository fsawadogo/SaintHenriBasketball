namespace SaintHenriBasketball.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public bool IsAdmin { get; set; }
}
