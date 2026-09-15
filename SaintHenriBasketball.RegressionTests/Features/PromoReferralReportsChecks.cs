using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.PromoReferralReports;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `promo-referral-reports` feature. Uses its own data; other checks share the database.
internal static class PromoReferralReportsChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        // A far-past window of our own, so range totals only see this check's payments and credits.
        var start = DateTime.UtcNow.Date.AddYears(-6).AddDays(-83).AddHours(5);
        var end = start.AddHours(2);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AppUrl"] = "http://localhost", ["Referrals:RewardAmount"] = "10.00" }).Build();
        ApplicationUser Player(string name) =>
            new($"prr_{name}_{tag}", $"prr-{name}-{tag}@example.test", "test-only", "Promo", $"{name} {tag}", PaymentPlan.DropIn) { EmailConfirmed = true };
        PromoReferralReportService Reports(ApplicationDbContext ctx) => new(new PromoReferralReportRepository(ctx));
        ReferralCodeAdminService CodeAdmin(ApplicationDbContext ctx) =>
            new(new ReferralRepository(ctx), new AuditLogService(new AuditLogRepository(ctx)), NullLogger<ReferralCodeAdminService>.Instance);
        ReferralService Referrals(ApplicationDbContext ctx)
        {
            var users = new UserRepository(ctx, NullLogger<UserRepository>.Instance);
            var flags = new FeatureFlagService(new FeatureFlagRepository(ctx),
                new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance),
                new AuditLogRepository(ctx), NullLogger<FeatureFlagService>.Instance);
            return new ReferralService(new ReferralRepository(ctx), users, NullLogger<ReferralService>.Instance, new PaymentRepository(ctx), flags,
                new NotificationService(new NotificationRepository(ctx), users, NullLogger<NotificationService>.Instance), new AuditLogRepository(ctx), config);
        }
        async Task<string?> RedeemAsync(Guid refereeId, string code)
        {
            await using var ctx = db();
            try { await Referrals(ctx).RedeemAsync(refereeId, code); return null; }
            catch (ValidationException ex) { return ex.Message; }
        }
        async Task<Exception?> UpdateCodeAsync(Guid codeId, UpdateReferralCodeDto body, Guid? adminId = null)
        {
            await using var ctx = db();
            try { await CodeAdmin(ctx).UpdateAsync(codeId, body, adminId, "PRR Admin"); return null; }
            catch (Exception ex) when (ex is ValidationException or NotFoundException) { return ex; }
        }

        // ---- Promo usage ----
        var payer = Player("payer");
        var promoUsed = new PromoCode($"PRR-USED-{tag}", PromoDiscountType.Fixed, 2m, start.AddDays(-10), DateTime.UtcNow.AddDays(30), PromoAppliesTo.Both) { TimesUsed = 4 };
        var promoIdle = new PromoCode($"PRR-IDLE-{tag}", PromoDiscountType.Percent, 50m, start.AddDays(-10), DateTime.UtcNow.AddDays(-1), PromoAppliesTo.Season, maxUses: 3, isActive: false);
        Payment PromoPayment(decimal original, decimal credit, PaymentStatus status, DateTime at) =>
            new(payer.Id, original - 2m - credit, PaymentPlan.DropIn)
            {
                Status = status, PaymentDate = at, CreatedAt = at, OriginalAmount = original, DiscountAmount = 2m, CreditApplied = credit,
                PromoCodeId = promoUsed.Id, Reference = $"PRR-{tag}",
            };
        await using (var ctx = db())
        {
            ctx.Users.Add(payer);
            ctx.PromoCodes.AddRange(promoUsed, promoIdle);
            ctx.Payments.AddRange(
                PromoPayment(10m, 0m, PaymentStatus.Completed, start.AddMinutes(10)),
                PromoPayment(20m, 5m, PaymentStatus.Completed, start.AddMinutes(50)),
                PromoPayment(10m, 0m, PaymentStatus.Refunded, start.AddMinutes(30)),
                PromoPayment(10m, 0m, PaymentStatus.Pending, start.AddMinutes(20)),
                PromoPayment(10m, 0m, PaymentStatus.Completed, start.AddDays(-1)));
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = db())
        {
            var windowed = await Reports(ctx).GetPromoUsageAsync(new ReportRangeQuery { From = start, To = end });
            var allTime = await Reports(ctx).GetPromoUsageAsync(new ReportRangeQuery());
            var used = windowed.Items.Single(r => r.PromoCodeId == promoUsed.Id);
            var idle = windowed.Items.Single(r => r.PromoCodeId == promoIdle.Id);
            assert(used is { TimesUsed: 2, RefundedUses: 1, TimesUsedCounter: 4, TotalListPrice: 30m, TotalDiscount: 4m, RevenueAfterDiscount: 26m, IsExpired: false, IsActive: true }
                && used.FirstUsedAt == start.AddMinutes(10) && used.LastUsedAt == start.AddMinutes(50)
                && used.FirstUsedAt?.Kind == DateTimeKind.Utc && used.ValidUntil.Kind == DateTimeKind.Utc,
                "promo usage counts completed payments in the range, excludes refunded and pending ones, and sums discount and revenue after discount");
            assert(idle is { TimesUsed: 0, RefundedUses: 0, TotalDiscount: 0m, RevenueAfterDiscount: 0m, IsExpired: true, IsActive: false, MaxUses: 3, FirstUsedAt: null, LastUsedAt: null },
                "an unused, expired, inactive promo code is still listed with zero uses");
            assert(windowed.Totals is { TimesUsed: 2, RefundedUses: 1, CodesUsed: 1, TotalListPrice: 30m, TotalDiscount: 4m, RevenueAfterDiscount: 26m }
                && windowed.Totals.PromoCodes == windowed.Items.Count && windowed.From?.Kind == DateTimeKind.Utc
                && allTime.Items.Single(r => r.PromoCodeId == promoUsed.Id).TimesUsed == 3 && !string.IsNullOrEmpty(windowed.UsageNote),
                "promo usage totals match the range, and without a range every completed use counts");
            var reversedRefused = false;
            try { await Reports(ctx).GetPromoUsageAsync(new ReportRangeQuery { From = end, To = start }); }
            catch (ValidationException ex) { reversedRefused = ex.Message == PromoReferralReportService.DateRangeMessage; }
            assert(reversedRefused, "promo usage report refuses a start date after the end date");
        }

        // ---- Referral codes: IsActive and MaxUses in the redeem flow ----
        var ownerInactive = Player("ownerinactive");
        var ownerLimit = Player("ownerlimit");
        var ownerRace = Player("ownerrace");
        var ownerRaw = Player("ownerraw");
        var ref1 = Player("ref1");
        var ref2 = Player("ref2");
        var ref3 = Player("ref3");
        var racers = Enumerable.Range(0, 5).Select(i => Player($"racer{i}")).ToArray();
        var inactiveCode = new ReferralCode($"PRRI{tag}", ownerInactive.Id) { IsActive = false };
        var limitCode = new ReferralCode($"PRRL{tag}", ownerLimit.Id, maxUses: 1);
        var raceCode = new ReferralCode($"PRRR{tag}", ownerRace.Id, maxUses: 2);
        var rawCodeId = Guid.NewGuid();
        await using (var ctx = db())
        {
            ctx.Users.AddRange(ownerInactive, ownerLimit, ownerRace, ownerRaw, ref1, ref2, ref3);
            ctx.Users.AddRange(racers);
            ctx.ReferralCodes.AddRange(inactiveCode, limitCode, raceCode);
            await ctx.SaveChangesAsync();
            // A row written without the new columns, as existing codes are when the migration adds them.
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ReferralCodes (Id, Code, OwnerUserId, TimesUsed, CreatedOn) VALUES ({rawCodeId}, {"PRRD" + tag}, {ownerRaw.Id}, 0, {DateTime.UtcNow})");
            var storedInactive = await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == inactiveCode.Id);
            var raw = await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == rawCodeId);
            assert(!storedInactive.IsActive && raw is { IsActive: true, MaxUses: null } && limitCode.IsActive,
                "an inactive referral code is saved as inactive, and codes default to active with unlimited uses");
        }

        var inactiveMessage = await RedeemAsync(ref1.Id, inactiveCode.Code.ToLowerInvariant());
        await using (var ctx = db())
            assert(inactiveMessage == ReferralService.InactiveCodeMessage && !await ctx.ReferralRedemptions.AnyAsync(r => r.RefereeUserId == ref1.Id)
                && (await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == inactiveCode.Id)).TimesUsed == 0,
                "redeeming an inactive referral code is refused with a clear message and counts nothing");

        var firstUse = await RedeemAsync(ref1.Id, limitCode.Code);
        var overLimit = await RedeemAsync(ref2.Id, limitCode.Code);
        await using (var ctx = db())
            assert(firstUse == null && overLimit == ReferralService.UsedUpCodeMessage
                && await ctx.ReferralRedemptions.CountAsync(r => r.ReferralCodeId == limitCode.Id) == 1
                && (await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == limitCode.Id)).TimesUsed == 1,
                "a referral code that reached MaxUses is refused with a clear message");

        // Race the atomic check itself: the service pre-check could refuse losers before they reach the database.
        var raceOutcomes = await Task.WhenAll(racers.Select(async racer =>
        {
            await using var ctx = db();
            return await new ReferralRepository(ctx).TryRedeemAsync(new ReferralRedemption(raceCode.Id, ownerRace.Id, racer.Id));
        }));
        ReferralRedeemOutcome inactiveOutcome;
        await using (var ctx = db())
            inactiveOutcome = await new ReferralRepository(ctx).TryRedeemAsync(new ReferralRedemption(inactiveCode.Id, ownerInactive.Id, ref3.Id));
        await using (var ctx = db())
            assert(raceOutcomes.Count(o => o == ReferralRedeemOutcome.Redeemed) == 2 && raceOutcomes.Count(o => o == ReferralRedeemOutcome.CodeUnavailable) == 3
                && (await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == raceCode.Id)).TimesUsed == 2
                && await ctx.ReferralRedemptions.CountAsync(r => r.ReferralCodeId == raceCode.Id) == 2
                && inactiveOutcome == ReferralRedeemOutcome.CodeInactive && !await ctx.ReferralRedemptions.AnyAsync(r => r.RefereeUserId == ref3.Id),
                "five concurrent redemptions of a two-use referral code record exactly two, and the atomic check also refuses an inactive code");

        // ---- Admin update ----
        var adminId = ref1.Id;
        var zeroMax = await UpdateCodeAsync(limitCode.Id, new UpdateReferralCodeDto { IsActive = true, MaxUses = 0 });
        var negativeMax = await UpdateCodeAsync(limitCode.Id, new UpdateReferralCodeDto { IsActive = true, MaxUses = -3 });
        var belowUses = await UpdateCodeAsync(raceCode.Id, new UpdateReferralCodeDto { IsActive = true, MaxUses = 1 });
        var unknown = await UpdateCodeAsync(Guid.NewGuid(), new UpdateReferralCodeDto { IsActive = true });
        await using (var ctx = db())
            assert(zeroMax is ValidationException { Message: ReferralCodeAdminService.MaxUsesMessage } && negativeMax is ValidationException { Message: ReferralCodeAdminService.MaxUsesMessage }
                && belowUses is ValidationException && belowUses.Message == ReferralCodeAdminService.BelowUsesMessage(2) && unknown is NotFoundException
                && (await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == raceCode.Id)).MaxUses == 2
                && (await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == limitCode.Id)).MaxUses == 1,
                "referral code update refuses MaxUses below 1, below current uses without confirmation, and unknown codes, changing nothing");

        ReferralCodeAdminDto atUses, belowConfirmed;
        await using (var ctx = db()) atUses = await CodeAdmin(ctx).UpdateAsync(raceCode.Id, new UpdateReferralCodeDto { IsActive = true, MaxUses = 2 }, adminId, "PRR Admin");
        await using (var ctx = db()) belowConfirmed = await CodeAdmin(ctx).UpdateAsync(raceCode.Id, new UpdateReferralCodeDto { IsActive = true, MaxUses = 1, AllowBelowCurrentUses = true }, adminId, "PRR Admin");
        var blockedByLowerLimit = await RedeemAsync(ref3.Id, raceCode.Code);
        await using (var ctx = db())
        {
            var audit = await ctx.AuditLogs.AsNoTracking().Where(a => a.EntityId == raceCode.Id && a.Action == "ReferralCode.Updated").ToListAsync();
            assert(atUses.MaxUses == 2 && belowConfirmed is { MaxUses: 1, TimesUsed: 2 } && blockedByLowerLimit == ReferralService.UsedUpCodeMessage
                && audit.Count == 2 && audit.Any(a => a.Details!.Contains("MaxUses: 2 -> 1") && a.Details.Contains("below current uses")),
                "MaxUses can equal current uses, and goes below them only when confirmed, which blocks new redemptions");
        }

        await using (var ctx = db()) await CodeAdmin(ctx).UpdateAsync(limitCode.Id, new UpdateReferralCodeDto { IsActive = false, MaxUses = null }, adminId, "PRR Admin");
        var afterDeactivation = await RedeemAsync(ref2.Id, limitCode.Code);
        await using (var ctx = db()) await CodeAdmin(ctx).UpdateAsync(limitCode.Id, new UpdateReferralCodeDto { IsActive = true, MaxUses = null }, adminId, "PRR Admin");
        var afterReactivation = await RedeemAsync(ref2.Id, limitCode.Code);
        await using (var ctx = db())
        {
            var audit = await ctx.AuditLogs.AsNoTracking().Where(a => a.EntityId == limitCode.Id && a.Action == "ReferralCode.Updated").OrderBy(a => a.CreatedAt).ToListAsync();
            assert(afterDeactivation == ReferralService.InactiveCodeMessage && afterReactivation == null
                && (await ctx.ReferralCodes.AsNoTracking().SingleAsync(c => c.Id == limitCode.Id)) is { IsActive: true, MaxUses: null, TimesUsed: 2 }
                && audit.Count == 2 && audit.All(a => a.UserId == adminId && a.UserName == "PRR Admin" && a.EntityType == nameof(ReferralCode))
                && audit.Any(a => a.Details!.Contains("IsActive: true -> false") && a.Details.Contains("MaxUses: 1 -> unlimited"))
                && audit.Any(a => a.Details!.Contains("IsActive: false -> true")),
                "deactivating a referral code blocks redemptions, reactivating with unlimited uses allows them, and each change is audited with before and after values");
        }

        // ---- Admin list ----
        await using (var ctx = db())
        {
            var referrals = Referrals(ctx);
            var granted = await ctx.ReferralRedemptions.AsNoTracking().SingleAsync(r => r.RefereeUserId == ref1.Id);
            var revoked = await ctx.ReferralRedemptions.AsNoTracking().SingleAsync(r => r.RefereeUserId == ref2.Id);
            await referrals.UpdateRedemptionStatusAsync(granted.Id, (int)ReferralRewardStatus.Granted, null, "Regression");
            await referrals.UpdateRedemptionStatusAsync(revoked.Id, (int)ReferralRewardStatus.Revoked, null, "Regression");
        }
        await using (var ctx = db())
        {
            var admin = CodeAdmin(ctx);
            var byTag = await admin.SearchAsync(new ReferralCodeAdminQuery { Search = tag, PageSize = 3 });
            var secondPage = await admin.SearchAsync(new ReferralCodeAdminQuery { Search = tag, PageSize = 3, Page = 2 });
            var byEmail = await admin.SearchAsync(new ReferralCodeAdminQuery { Search = ownerLimit.Email });
            var byName = await admin.SearchAsync(new ReferralCodeAdminQuery { Search = $"Promo ownerrace {tag.ToLowerInvariant()}" });
            var limitRow = byEmail.Items.SingleOrDefault();
            var raceRow = byName.Items.SingleOrDefault();
            assert(byTag.Total == 4 && byTag.Items.Count == 3 && secondPage.Items.Count == 1
                && byTag.Items.Concat(secondPage.Items).Select(i => i.Id).Distinct().Count() == 4
                && byEmail.Total == 1 && limitRow is { IsActive: true, MaxUses: null, TimesUsed: 2, RewardsGrantedCount: 1, RewardsGrantedTotal: 10m }
                && limitRow.Id == limitCode.Id && limitRow.OwnerName == $"Promo ownerlimit {tag}" && limitRow.OwnerEmail == ownerLimit.Email
                && limitRow.Redemptions is { Pending: 0, Granted: 1, Revoked: 1, Total: 2 } && limitRow.CreatedOn.Kind == DateTimeKind.Utc
                && byName.Total == 1 && raceRow is { MaxUses: 1, RewardsGrantedCount: 0, RewardsGrantedTotal: 0m } && raceRow.Redemptions is { Pending: 2, Total: 2 },
                "referral code list pages, searches by code, owner email or name, and shows uses by status and rewards granted");
        }

        // ---- Credits report ----
        var c1 = Player("credit1");
        var c2 = Player("credit2");
        var c3 = Player("credit3");
        await using (var ctx = db())
        {
            ctx.Users.AddRange(c1, c2, c3);
            void Credit(ApplicationUser user, decimal amount, AccountCreditKind kind, DateTime at)
            {
                var entry = new AccountCredit(user.Id, amount, kind, kind == AccountCreditKind.ReferralReward ? Guid.NewGuid() : null, note: $"PRR {tag}");
                ctx.AccountCredits.Add(entry);
                ctx.Entry(entry).Property(e => e.CreatedAt).CurrentValue = at;
            }
            Credit(c1, 10m, AccountCreditKind.ReferralReward, start.AddMinutes(5));
            Credit(c1, -4m, AccountCreditKind.AppliedToPayment, start.AddMinutes(15));
            Credit(c1, 1.5m, AccountCreditKind.Released, start.AddMinutes(20));
            Credit(c1, 3m, AccountCreditKind.ManualAdjustment, start.AddMinutes(25));
            Credit(c1, -2m, AccountCreditKind.ManualAdjustment, start.AddMinutes(30));
            Credit(c2, 8m, AccountCreditKind.Refund, start.AddMinutes(35));
            Credit(c2, -8m, AccountCreditKind.AppliedToPayment, start.AddMinutes(40));
            Credit(c3, 5m, AccountCreditKind.ManualAdjustment, start.AddMinutes(45));
            Credit(c3, 7m, AccountCreditKind.ReferralReward, start.AddDays(-1));
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = db())
        {
            var reports = Reports(ctx);
            var report = await reports.GetCreditsReportAsync(new ReportRangeQuery { From = start, To = end });
            var before = await reports.GetCreditsReportAsync(new ReportRangeQuery { To = start.AddTicks(-1) });
            CreditKindTotalDto? Granted(AccountCreditKind kind) => report.Granted.SingleOrDefault(g => g.Kind == kind);
            assert(report.Granted.Count == 4 && Granted(AccountCreditKind.ReferralReward) is { Count: 1, Total: 10m, KindName: "ReferralReward" }
                && Granted(AccountCreditKind.Released) is { Count: 1, Total: 1.5m } && Granted(AccountCreditKind.Refund) is { Count: 1, Total: 8m }
                && Granted(AccountCreditKind.ManualAdjustment) is { Count: 2, Total: 8m } && report.GrantedTotal == 27.5m,
                "credits report groups credits granted in the range by kind");
            assert(report.Spent is { Count: 2, Total: 12m } && report.RefundsAsCredit is { Count: 1, Total: 8m }
                && report.ManualAdjustments is { Added.Count: 2, Added.Total: 8m, Removed.Count: 1, Removed.Total: 2m, Net: 6m },
                "credits report totals credit spent on payments, refunds issued as credit and manual adjustments both ways");
            assert(report.Outstanding.Balance - before.Outstanding.Balance == 13.5m && report.Outstanding.PlayersWithBalance - before.Outstanding.PlayersWithBalance == 1
                && report.Outstanding.AsOf == end && report.Outstanding.AsOf.Kind == DateTimeKind.Utc,
                "outstanding credit is the positive balances at the end of the range, with the number of players holding one");
            var reversedRefused = false;
            try { await reports.GetCreditsReportAsync(new ReportRangeQuery { From = end, To = start }); }
            catch (ValidationException) { reversedRefused = true; }
            assert(reversedRefused, "credits report refuses a start date after the end date");
        }
    }
}
