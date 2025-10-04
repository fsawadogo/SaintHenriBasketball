namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AddParticipantsResponseDto
{
    public int TotalRequested { get; set; }
    public int SuccessfullyAdded { get; set; }
    public int AlreadyRegistered { get; set; }
    public int Failed { get; set; }
    public List<Guid> SuccessfullyAddedUserIds { get; set; } = new();
    public List<Guid> AlreadyRegisteredUserIds { get; set; } = new();
    public List<ParticipantAddFailureDto> FailedAdditions { get; set; } = new();
    public string? Message { get; set; }
}

public class ParticipantAddFailureDto
{
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
