namespace SaintHenriBasketball.Application.Services.Interfaces;

/// What one purge run removed, or would remove.
public class UnconfirmedPurgeResultDto
{
    public bool DryRun { get; set; }
    public int Matched { get; set; }
    public int Deleted { get; set; }
    /// Accounts that matched on age and confirmation but were kept because they have history.
    public int KeptWithHistory { get; set; }
    /// Who would go, on a dry run: "name — email — joined".
    public List<string> Accounts { get; set; } = new();
}

/// <summary>
/// Removes signups that never confirmed their address and never did anything.
///
/// Such an account cannot sign in, so it is only clutter — until a bot makes a hundred of them.
/// Anything with a payment, a booking, attendance or a waitlist place is kept regardless: that is
/// a real person whose confirmation mail went astray, not litter.
/// </summary>
public interface IUnconfirmedAccountPurgeService
{
    Task<UnconfirmedPurgeResultDto> RunAsync(int olderThanDays, bool dryRun = true);
}
