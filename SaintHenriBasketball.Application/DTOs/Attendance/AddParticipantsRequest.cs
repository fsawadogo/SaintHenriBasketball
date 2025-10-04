namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AddParticipantsRequest
{
    public List<Guid> UserIds { get; set; } = new();
    public bool IsAttending { get; set; } = true;
    public string? Notes { get; set; }
    public string? AdminNotes { get; set; }
}
