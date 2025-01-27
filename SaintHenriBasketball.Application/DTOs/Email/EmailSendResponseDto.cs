namespace SaintHenriBasketball.Application.DTOs.Email;

public class EmailSendResponseDto
{
    public string? Message { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string?>? FailedEmails { get; set; }
}