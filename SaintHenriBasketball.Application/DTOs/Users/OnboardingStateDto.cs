namespace SaintHenriBasketball.Application.DTOs.Users;

/// What the app needs to decide whether to show a player the welcome tour.
public class OnboardingStateDto
{
    /// True once the player has asked not to see the welcome tour again.
    public bool PlayerTourDismissed { get; set; }
}
