namespace SaintHenriBasketball.Domain.Enums;

public enum InteracMatchConfidence
{
    /// Nothing matched.
    None,
    /// The player's claimed reference and the amount both agree: safe to complete without asking.
    Exact,
    /// Amount and name agree but no reference does: an admin confirms before the money is counted.
    Likely
}
