using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Referrals;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class ReferralService : IReferralService
{
    private const int CodeLength = 8;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // omit 0/O/1/I

    private readonly IReferralRepository _referralRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(
        IReferralRepository referralRepository,
        IUserRepository userRepository,
        ILogger<ReferralService> logger)
    {
        _referralRepository = referralRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ReferralCodeDto> GetOrCreateOwnCodeAsync(Guid userId, string shareBaseUrl)
    {
        var existing = await _referralRepository.GetCodeByOwnerAsync(userId);
        if (existing is null)
        {
            var value = await GenerateUniqueCodeAsync();
            existing = new ReferralCode(value, userId);
            await _referralRepository.AddCodeAsync(existing);
        }

        return ToDto(existing, shareBaseUrl);
    }

    public async Task RedeemAsync(Guid refereeUserId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Referral code is required");

        var normalizedCode = code.Trim().ToUpperInvariant();
        var referralCode = await _referralRepository.GetCodeByValueAsync(normalizedCode)
            ?? throw new NotFoundException("Referral code not found");

        if (referralCode.OwnerUserId == refereeUserId)
            throw new ValidationException("You cannot redeem your own code");

        if (referralCode.MaxUses is int max && referralCode.TimesUsed >= max)
            throw new ValidationException("This referral code has been used up");

        if (await _referralRepository.HasRefereeRedeemedAsync(refereeUserId))
            throw new ValidationException("You have already redeemed a referral code");

        var redemption = new ReferralRedemption(referralCode.Id, referralCode.OwnerUserId, refereeUserId);
        await _referralRepository.AddRedemptionAsync(redemption);

        referralCode.TimesUsed++;
        await _referralRepository.UpdateCodeAsync(referralCode);

        _logger.LogInformation(
            "Referral code {Code} redeemed by {RefereeId} (referrer {ReferrerId})",
            normalizedCode, refereeUserId, referralCode.OwnerUserId);
    }

    public async Task<IReadOnlyList<ReferralRedemptionDto>> GetRedemptionsAsync(int page = 1, int pageSize = 50)
    {
        var redemptions = await _referralRepository.GetRedemptionsAsync(page, pageSize);
        var userIds = redemptions
            .SelectMany(r => new[] { r.ReferrerUserId, r.RefereeUserId })
            .Distinct()
            .ToList();
        var users = (await _userRepository.GetUsersByIdsAsync(userIds)).ToDictionary(u => u.Id);

        return redemptions.Select(r => new ReferralRedemptionDto
        {
            Id = r.Id,
            ReferrerUserId = r.ReferrerUserId,
            ReferrerName = NameOf(users, r.ReferrerUserId),
            RefereeUserId = r.RefereeUserId,
            RefereeName = NameOf(users, r.RefereeUserId),
            RewardStatus = (int)r.RewardStatus,
            RedeemedOn = r.RedeemedOn,
        }).ToList();
    }

    public async Task UpdateRedemptionStatusAsync(Guid redemptionId, int newStatus)
    {
        if (!Enum.IsDefined(typeof(ReferralRewardStatus), newStatus))
            throw new ValidationException("Invalid status");

        var redemption = await _referralRepository.GetRedemptionByIdAsync(redemptionId)
            ?? throw new NotFoundException($"Redemption {redemptionId} not found");

        redemption.RewardStatus = (ReferralRewardStatus)newStatus;
        await _referralRepository.UpdateRedemptionAsync(redemption);
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = GenerateCandidate();
            if (await _referralRepository.GetCodeByValueAsync(candidate) is null)
                return candidate;
        }
        throw new InvalidOperationException("Could not generate a unique referral code");
    }

    private static string GenerateCandidate()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);
        return new string(bytes.Select(b => CodeAlphabet[b % CodeAlphabet.Length]).ToArray());
    }

    private static string NameOf(IReadOnlyDictionary<Guid, ApplicationUser> users, Guid id) =>
        users.TryGetValue(id, out var u) ? $"{u.FirstName} {u.LastName}".Trim() : "(unknown)";

    private static ReferralCodeDto ToDto(ReferralCode code, string shareBaseUrl) => new()
    {
        Code = code.Code,
        TimesUsed = code.TimesUsed,
        MaxUses = code.MaxUses,
        ShareUrl = $"{shareBaseUrl.TrimEnd('/')}/register?ref={Uri.EscapeDataString(code.Code)}",
    };
}
