namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class RemoveParticipantsRequest
{
    public List<Guid> UserIds { get; set; } = new();
    public string? Reason { get; set; }
    public string? AdminNotes { get; set; }
}
