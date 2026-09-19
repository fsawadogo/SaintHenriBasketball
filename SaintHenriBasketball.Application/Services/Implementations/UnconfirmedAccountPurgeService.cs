using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class UnconfirmedAccountPurgeService : IUnconfirmedAccountPurgeService
{
    /// Never sweep something that only signed up this morning: a person can take a day to find the
    /// email, and their account is harmless until they do.
    public const int MinimumAgeDays = 2;

    private readonly IUnconfirmedAccountRepository _accounts;
    private readonly ILogger<UnconfirmedAccountPurgeService> _logger;

    public UnconfirmedAccountPurgeService(
        IUnconfirmedAccountRepository accounts,
        ILogger<UnconfirmedAccountPurgeService> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<UnconfirmedPurgeResultDto> RunAsync(int olderThanDays, DateTime? createdAfter = null, bool dryRun = true)
    {
        var age = Math.Max(MinimumAgeDays, olderThanDays);
        var cutoff = DateTime.UtcNow.AddDays(-age);

        var candidates = await _accounts.GetPurgeableAsync(cutoff, createdAfter);
        var result = new UnconfirmedPurgeResultDto { DryRun = dryRun, Matched = candidates.Count };

        foreach (var account in candidates)
        {
            if (account.HasHistory)
            {
                result.KeptWithHistory++;
                continue;
            }

            if (result.Accounts.Count < 500)
                result.Accounts.Add($"{account.Name} — {account.Email} — {account.CreatedOn:yyyy-MM-dd}");

            if (!dryRun) await _accounts.DeleteAsync(account.Id);
            result.Deleted++;
        }

        _logger.LogInformation(
            "Unconfirmed purge ({Mode}) over {From}..{To}: {Matched} matched, {Deleted} removed, {Kept} kept for having history",
            dryRun ? "dry run" : "live", createdAfter, cutoff, result.Matched, result.Deleted, result.KeptWithHistory);

        return result;
    }
}
