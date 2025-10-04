namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class RemoveParticipantsResponseDto
{
    public int TotalRequested { get; set; }
    public int SuccessfullyRemoved { get; set; }
    public int NotRegistered { get; set; }
    public int Failed { get; set; }
    public List<Guid> SuccessfullyRemovedUserIds { get; set; } = new();
    public List<Guid> NotRegisteredUserIds { get; set; } = new();
    public List<ParticipantRemoveFailureDto> FailedRemovals { get; set; } = new();
    public string? Message { get; set; }
}

public class ParticipantRemoveFailureDto
{
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
