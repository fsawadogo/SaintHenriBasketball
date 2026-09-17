using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.Helpers;

/// Which season a given date belongs to.
///
/// Deliberately total and deliberately unhelpful when the answer is unclear: a session can fall
/// outside every season (the off-season), and seasons can overlap — season rollover creates a new
/// season while the old one may still exist, and the wizard reports an overlap as a warning rather
/// than a block. Both existing "which season is it" resolvers in this codebase already bail out in
/// that case rather than guess, and so does this one.
///
/// Callers that spend money on the answer MUST treat null as "do not act", never as a default.
public static class SeasonForDate
{
    /// The one season whose dates contain <paramref name="date"/>, or null when none or several do.
    /// Compared by calendar day, inclusive at both ends.
    public static Season? Resolve(IEnumerable<Season> seasons, DateTime date)
    {
        var day = date.Date;
        Season? found = null;

        foreach (var season in seasons)
        {
            if (season.StartDate.Date > day || season.EndDate.Date < day) continue;
            // A second match makes the answer ambiguous; stop and report nothing.
            if (found is not null) return null;
            found = season;
        }

        return found;
    }

    /// Why <see cref="Resolve"/> returned null, for a log line an operator can act on.
    public static string Explain(IEnumerable<Season> seasons, DateTime date)
    {
        var day = date.Date;
        var matches = seasons.Count(s => s.StartDate.Date <= day && s.EndDate.Date >= day);
        return matches == 0
            ? "no season covers this date"
            : $"{matches} seasons overlap this date";
    }
}
