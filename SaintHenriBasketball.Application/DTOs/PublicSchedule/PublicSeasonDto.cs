namespace SaintHenriBasketball.Application.DTOs.PublicSchedule;

/// The open season as a visitor with no account may see it.
///
/// Deliberately its own shape, not SeasonDto: SeasonDto carries every registered player's name and
/// email, so SeasonsController cannot simply be opened to anonymous callers.
public class PublicSeasonDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }

    /// Season pass spots. Null while season-plan-choice is off, because the cap is not enforced then
    /// and advertising "7 of 15 left" would be untrue.
    public int? Capacity { get; set; }
    public int? SpotsLeft { get; set; }
}
