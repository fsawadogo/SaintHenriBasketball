using SaintHenriBasketball.Application.DTOs.Stats;
using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// Static catalog of badges earned by attendance patterns. Consumed by PersonalStatsService
/// and (in Phase 2.2) the streaks-badges feature on the dashboard.
internal static class BadgeCatalog
{
    public static List<BadgeDto> ComputeEarned(int totalAttended, int longestStreak, IReadOnlyList<SessionAttendance> attendedOrdered)
    {
        var earned = new List<BadgeDto>();

        void Earn(string key, string labelEn, string labelFr, string descEn, string descFr, DateTime? earnedOn)
        {
            earned.Add(new BadgeDto
            {
                Key = key,
                LabelEn = labelEn,
                LabelFr = labelFr,
                DescriptionEn = descEn,
                DescriptionFr = descFr,
                EarnedOn = earnedOn,
            });
        }

        if (totalAttended >= 1)
            Earn("first-session", "First session", "Première séance",
                "Your first attended session at SHB.",
                "Votre première séance confirmée au SHB.",
                attendedOrdered.First().Session?.SessionDate);

        if (longestStreak >= 5)
            Earn("five-in-a-row", "Five in a row", "Cinq de suite",
                "Attended five registered sessions back-to-back.",
                "Présent à cinq séances inscrites consécutives.", null);

        if (longestStreak >= 10)
            Earn("ten-in-a-row", "Ten in a row", "Dix de suite",
                "Ten consecutive attended sessions — rock solid.",
                "Dix séances consécutives — solide.", null);

        if (totalAttended >= 25)
            Earn("regular", "Regular", "Habitué",
                "Twenty-five sessions in the books.",
                "Vingt-cinq séances à votre actif.", null);

        if (totalAttended >= 50)
            Earn("veteran", "Veteran", "Vétéran",
                "Fifty attended sessions. SHB family.",
                "Cinquante séances. Membre de la famille SHB.", null);

        return earned;
    }
}
