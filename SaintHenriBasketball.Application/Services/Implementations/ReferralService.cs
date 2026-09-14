using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Referrals;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class ReferralService : IReferralService
{
    private const int CodeLength = 8;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // omit 0/O/1/I
    private const decimal DefaultRewardAmount = 10.00m;
    private const int RedeemWindowDays = 30;

    private readonly IReferralRepository _referralRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ReferralService> _logger;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IConfiguration _configuration;

    public ReferralService(
        IReferralRepository referralRepository,
        IUserRepository userRepository,
        ILogger<ReferralService> logger,
        IPaymentRepository paymentRepository,
        IFeatureFlagService featureFlagService,
        INotificationService notificationService,
        IAuditLogRepository auditLogRepository,
        IConfiguration configuration)
    {
        _referralRepository = referralRepository;
        _userRepository = userRepository;
        _logger = logger;
        _paymentRepository = paymentRepository;
        _featureFlagService = featureFlagService;
        _notificationService = notificationService;
        _auditLogRepository = auditLogRepository;
        _configuration = configuration;
    }

    /// Credit (CAD) per granted referral, from <c>Referrals:RewardAmount</c>.
    private decimal RewardAmount
    {
        get
        {
            var raw = _configuration["Referrals:RewardAmount"];
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
                return PaymentPricing.RoundCents(amount);
            if (raw != null)
                _logger.LogWarning("Invalid Referrals:RewardAmount {Value}; using {Default}", raw, DefaultRewardAmount);
            return DefaultRewardAmount;
        }
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

        return ToDto(existing, shareBaseUrl, RewardAmount);
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

        var referee = await _userRepository.GetByIdAsync(refereeUserId)
            ?? throw new NotFoundException("User not found");
        if (referee.CreatedOn < DateTime.UtcNow.AddDays(-RedeemWindowDays))
            throw new ValidationException($"Referral codes can only be redeemed within {RedeemWindowDays} days of creating your account");
        if (await _paymentRepository.HasCompletedPaymentAsync(refereeUserId))
            throw new ValidationException("Referral codes are only for new players who have not made a payment yet");
        if (await _referralRepository.HasReferredAsync(refereeUserId, referralCode.OwnerUserId))
            throw new ValidationException("You cannot redeem the code of a player you referred");

        var outcome = await _referralRepository.TryRedeemAsync(
            new ReferralRedemption(referralCode.Id, referralCode.OwnerUserId, refereeUserId));
        switch (outcome)
        {
            case ReferralRedeemOutcome.CodeUnavailable:
                throw new ValidationException("This referral code has been used up");
            case ReferralRedeemOutcome.AlreadyRedeemed:
                throw new ValidationException("You have already redeemed a referral code");
        }

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

    public async Task UpdateRedemptionStatusAsync(Guid redemptionId, int newStatus, Guid? adminId = null, string adminName = "Admin")
    {
        if (!Enum.IsDefined(typeof(ReferralRewardStatus), newStatus))
            throw new ValidationException("Invalid status");

        var redemption = await _referralRepository.GetRedemptionByIdAsync(redemptionId)
            ?? throw new NotFoundException($"Redemption {redemptionId} not found");

        var target = (ReferralRewardStatus)newStatus;
        if (redemption.RewardStatus == target) return;

        switch (target)
        {
            case ReferralRewardStatus.Granted:
                // Same path as a first payment, so the credit is created exactly once.
                if (!await GrantAsync(redemptionId, "Granted by admin", adminId, adminName))
                    throw new ValidationException("Only pending referrals can be granted");
                break;
            case ReferralRewardStatus.Revoked:
                if (!await _referralRepository.TryRevokeAsync(redemptionId))
                    throw new ValidationException("Only pending referrals can be revoked");
                await WriteAuditAsync("Referral.RewardRevoked", redemptionId,
                    $"Referrer: {redemption.ReferrerUserId}, Referee: {redemption.RefereeUserId}", adminId, adminName);
                break;
            default:
                throw new ValidationException("A referral cannot be moved back to pending");
        }
    }

    public async Task GrantRewardForPaymentAsync(Guid payerUserId, Guid paymentId, decimal amount, DateTime completedAt)
    {
        try
        {
            if (amount <= 0) return;
            if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Referrals)) return;

            var redemption = await _referralRepository.GetRedemptionByRefereeAsync(payerUserId);
            if (redemption is not { RewardStatus: ReferralRewardStatus.Pending }) return;

            // Only the payer's first paid payment earns the reward. "Earlier" rather than "any other" so
            // two payments completing at the same moment cannot each defer to the other.
            if (await _paymentRepository.HasEarlierPaidPaymentAsync(payerUserId, paymentId, completedAt)) return;

            await GrantAsync(redemption.Id, $"First payment {paymentId}", null, "System");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Referral reward check failed for payment {PaymentId}", paymentId);
        }
    }

    /// Grants the reward credit once, then notifies the referrer and audits. False when nothing was granted.
    private async Task<bool> GrantAsync(Guid redemptionId, string trigger, Guid? actorId, string actorName)
    {
        var rewardAmount = RewardAmount;
        var granted = await _referralRepository.TryGrantRewardAsync(redemptionId, rewardAmount);
        if (granted is null) return false;

        _logger.LogInformation("Referral {RedemptionId} granted: {Amount} credit to {ReferrerId} ({Trigger})",
            redemptionId, rewardAmount, granted.ReferrerUserId, trigger);

        var referrer = await _userRepository.GetByIdAsync(granted.ReferrerUserId);
        var language = referrer?.PreferredLanguage ?? EmailLanguage.English;
        // NotificationService swallows its own failures.
        await _notificationService.CreateAsync(
            granted.ReferrerUserId,
            NotificationType.Generic,
            title: EmailTemplateHelper.L("Referral reward earned", "Récompense de parrainage obtenue", language),
            body: EmailTemplateHelper.L(
                $"A player you referred made their first payment. ${rewardAmount:F2} in credit was added to your account.",
                $"Un joueur que vous avez parrainé a effectué son premier paiement. Un crédit de {rewardAmount.ToString("F2", CultureInfo.GetCultureInfo("fr-CA"))} $ a été ajouté à votre compte.",
                language));

        await WriteAuditAsync("Referral.RewardGranted", redemptionId,
            $"Referrer: {granted.ReferrerUserId}, Referee: {granted.RefereeUserId}, Credit: {rewardAmount:0.00}, Trigger: {trigger}",
            actorId, actorName);
        return true;
    }

    private async Task WriteAuditAsync(string action, Guid redemptionId, string details, Guid? actorId, string actorName)
    {
        try
        {
            await _auditLogRepository.AddAsync(new AuditLog(action, nameof(ReferralRedemption), redemptionId, details, actorId, actorName));
        }
        catch (Exception ex)
        {
            // The status change is already committed; a missing audit row must not report it as failed.
            _logger.LogWarning(ex, "Audit entry {Action} failed for referral {RedemptionId}", action, redemptionId);
        }
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

    private static ReferralCodeDto ToDto(ReferralCode code, string shareBaseUrl, decimal rewardAmount) => new()
    {
        Code = code.Code,
        TimesUsed = code.TimesUsed,
        MaxUses = code.MaxUses,
        ShareUrl = $"{shareBaseUrl.TrimEnd('/')}/register?ref={Uri.EscapeDataString(code.Code)}",
        RewardAmount = rewardAmount,
    };
}
