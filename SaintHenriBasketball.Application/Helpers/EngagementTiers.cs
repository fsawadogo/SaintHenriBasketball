namespace SaintHenriBasketball.Application.Helpers;

/// Player engagement: the share of the last 60 days' sessions a player attended.
public static class EngagementTiers
{
    public const int WindowDays = 60;
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = new[] { High, Medium, Low, Inactive };

    public static double Rate(int attended, int sessions) => sessions > 0 ? (double)attended / sessions * 100 : 0;

    public static string Tier(double rate) => rate >= 80 ? High : rate >= 50 ? Medium : rate >= 20 ? Low : Inactive;

    /// The attended-session counts that land in <paramref name="tier"/> when the window had <paramref name="sessions"/>
    /// sessions, as (minimum, exclusive maximum) with null meaning no upper bound. Null for an unknown tier.
    public static (int Min, int? MaxExclusive)? AttendedRange(string tier, int sessions)
    {
        if (!All.Contains(tier)) return null;
        if (sessions <= 0) return tier == Inactive ? (0, null) : (int.MaxValue, null);

        // Smallest whole number of sessions whose rate reaches the percentage.
        int AtLeast(int percent) => (percent * sessions + 99) / 100;

        if (tier == High) return (AtLeast(80), null);
        if (tier == Medium) return (AtLeast(50), AtLeast(80));
        if (tier == Low) return (AtLeast(20), AtLeast(50));
        return (0, AtLeast(20));
    }
}
