namespace SaintHenriBasketball.Application.DTOs.Session;

/// What deleting a session removes. A session with payments can't be deleted; it should be cancelled instead.
public class SessionDeletionPreviewDto
{
    public Guid SessionId { get; set; }
    public int Registrations { get; set; }
    public int AttendanceAnswers { get; set; }
    public int Waitlisted { get; set; }
    public int Feedback { get; set; }
    public int Recaps { get; set; }
    public int Payments { get; set; }
    public bool CanDelete { get; set; }
    public string? BlockedReason { get; set; }
}
