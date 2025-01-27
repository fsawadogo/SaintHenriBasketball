namespace SaintHenriBasketball.Application.DTOs.Users;

public class EmailSendResult
{
    public bool AllSucceeded => FailedEmails.Count == 0;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string?> FailedEmails { get; set; } = new();
}
