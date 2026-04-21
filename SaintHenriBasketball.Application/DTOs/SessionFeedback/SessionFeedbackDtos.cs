namespace SaintHenriBasketball.Application.DTOs.SessionFeedback;

public class SubmitSessionFeedbackDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class PendingFeedbackDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public string? StartTime { get; set; }
    public string? Location { get; set; }
}

public class SessionFeedbackDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class SessionFeedbackSummaryDto
{
    public int Count { get; set; }
    public double AverageRating { get; set; }
    public IReadOnlyList<SessionFeedbackDto> Items { get; set; } = Array.Empty<SessionFeedbackDto>();
}
