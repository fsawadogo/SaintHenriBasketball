namespace SaintHenriBasketball.Application.DTOs.QrCheckIn;

public class SessionQrTokenDto
{
    public Guid SessionId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string CheckInUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class QrCheckInRequestDto
{
    public string Token { get; set; } = string.Empty;
}

public class QrCheckInResultDto
{
    public Guid SessionId { get; set; }
    public DateTime CheckedInAt { get; set; }
}
