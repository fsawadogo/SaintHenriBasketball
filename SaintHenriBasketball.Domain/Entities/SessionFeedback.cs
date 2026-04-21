namespace SaintHenriBasketball.Domain.Entities;

public class SessionFeedback
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; set; }
    public DateTime CreatedOn { get; private set; }

    private SessionFeedback() { } // EF Core

    public SessionFeedback(Guid sessionId, Guid userId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");

        Id = Guid.NewGuid();
        SessionId = sessionId;
        UserId = userId;
        Rating = rating;
        Comment = comment;
        CreatedOn = DateTime.UtcNow;
    }
}
