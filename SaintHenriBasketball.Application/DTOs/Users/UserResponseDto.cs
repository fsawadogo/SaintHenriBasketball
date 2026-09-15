using System.Text.Json.Serialization;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class UserResponseDto
{
    public required string Token { get; set; }
    public required string? Username { get; set; }
    public required string? Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public bool IsAdmin { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public bool Requires2Fa { get; set; }
    /// An admin must set up two-factor authentication before using the app (admin-2fa is on).
    public bool Requires2FaSetup { get; set; }
    /// Volunteer role as its name: "None", "CourtCaptain" or "Treasurer".
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StaffRole StaffRole { get; set; }
}