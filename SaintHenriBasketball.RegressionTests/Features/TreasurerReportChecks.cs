using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaintHenriBasketball.Application.DTOs.TreasurerReport;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

/// Regression checks for the `treasurer-report` feature. Uses its own data; other checks share the database.
/// All payments sit in October-December 2004 (Toronto time), a window no other check writes to, so totals are exact.
internal static class TreasurerReportChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        TreasurerReportService ServiceFor(ApplicationDbContext context) => new(
            new TreasurerReportRepository(context),
            new SeasonRepository(context, NullLogger<SeasonRepository>.Instance),
            new AuditLogService(new AuditLogRepository(context)));

        var tag = Guid.NewGuid().ToString("N")[..8];
        var zone = TreasurerReportService.TorontoTimeZone;
        DateTime Local(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified), zone);
        DateTime Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        var season = new Season(new DateTime(2004, 9, 1), new DateTime(2004, 12, 31), 100m) { Name = $"Automne Été {tag}" };
        var seasonPlayer = new ApplicationUser($"tr_elodie_{tag}", $"tr-elodie-{tag}@example.test", "test-only", "Élodie", "Trésorière", PaymentPlan.Season) { EmailConfirmed = true };
        var dropInPlayer = new ApplicationUser($"tr_formula_{tag}", $"tr-formula-{tag}@example.test", "test-only", "=cmd", "Formula", PaymentPlan.DropIn) { EmailConfirmed = true };

        Payment Pay(ApplicationUser user, decimal amount, PaymentPlan plan, PaymentStatus status, DateTime paymentDate, string reference) =>
            new(user.Id, amount, plan)
            {
                Status = status,
                PaymentDate = paymentDate,
                CreatedAt = paymentDate,
                Reference = reference,
                SeasonId = plan == PaymentPlan.Season ? season.Id : null,
            };

        var cardCompleted = Pay(dropInPlayer, 10m, PaymentPlan.DropIn, PaymentStatus.Completed, Utc(2004, 10, 5, 15), $"cs_tr{tag}_1");
        cardCompleted.OriginalAmount = 12m;
        cardCompleted.DiscountAmount = 2m;
        var interacSeason = Pay(seasonPlayer, 90m, PaymentPlan.Season, PaymentStatus.Completed, Utc(2004, 10, 10, 15), $"SEASON-TR{tag}2|INTERAC:BANK-{tag}-2");
        interacSeason.OriginalAmount = 100m;
        interacSeason.CreditApplied = 10m;
        var creditCovered = Pay(dropInPlayer, 0m, PaymentPlan.DropIn, PaymentStatus.Completed, Utc(2004, 11, 2, 15), $"DROPIN-TR{tag}3");
        creditCovered.OriginalAmount = 10m;
        creditCovered.CreditApplied = 10m;
        // 03:30 UTC on 1 November is 22:30 on 31 October in Toronto (EST, after DST ended that morning).
        var monthBoundary = Pay(dropInPlayer, 15m, PaymentPlan.DropIn, PaymentStatus.Completed, Utc(2004, 11, 1, 3, 30), $"DROPIN-TR{tag}4|INTERAC:BANK-{tag}-4");
        var cardPending = Pay(dropInPlayer, 10m, PaymentPlan.DropIn, PaymentStatus.Pending, Utc(2004, 11, 10, 15), $"cs_tr{tag}_5");
        var interacPending = Pay(seasonPlayer, 100m, PaymentPlan.Season, PaymentStatus.Pending, Utc(2004, 11, 11, 15), $"SEASON-TR{tag}6|INTERAC:BANK-{tag}-6");
        var refundedCard = Pay(dropInPlayer, 20m, PaymentPlan.DropIn, PaymentStatus.Refunded, Utc(2004, 10, 20, 15), $"cs_tr{tag}_7");
        refundedCard.DiscountAmount = 5m;
        refundedCard.OriginalAmount = 25m;
        refundedCard.RefundMethod = RefundMethod.Card;
        refundedCard.RefundedOn = Utc(2004, 11, 5, 15);
        var refundedCredit = Pay(seasonPlayer, 50m, PaymentPlan.Season, PaymentStatus.Refunded, Utc(2004, 10, 21, 15), $"SEASON-TR{tag}8|INTERAC:BANK-{tag}-8");
        refundedCredit.RefundMethod = RefundMethod.AccountCredit;
        refundedCredit.RefundedOn = Utc(2004, 10, 25, 15);
        var refundedManual = Pay(dropInPlayer, 7m, PaymentPlan.DropIn, PaymentStatus.Refunded, Utc(2004, 11, 3, 15), $"DROPIN-TR{tag}9|INTERAC:BANK-{tag}-9");
        refundedManual.RefundMethod = RefundMethod.Manual;
        refundedManual.RefundedOn = Utc(2004, 11, 4, 15);
        var failed = Pay(dropInPlayer, 99m, PaymentPlan.DropIn, PaymentStatus.Failed, Utc(2004, 10, 15, 15), $"DROPIN-TR{tag}10");
        var afterRange = Pay(dropInPlayer, 1000m, PaymentPlan.DropIn, PaymentStatus.Completed, Utc(2004, 12, 15, 15), $"DROPIN-TR{tag}11");
        var seasonAfterRange = Pay(seasonPlayer, 100m, PaymentPlan.Season, PaymentStatus.Completed, Utc(2004, 12, 20, 15), $"SEASON-TR{tag}12|INTERAC:BANK-{tag}-12");
        // 03:59 UTC on 1 October is 23:59 on 30 September in Toronto: just before the range.
        var beforeRange = Pay(dropInPlayer, 500m, PaymentPlan.DropIn, PaymentStatus.Completed, Utc(2004, 10, 1, 3, 59), $"DROPIN-TR{tag}13");

        await using (var context = db())
        {
            context.Seasons.Add(season);
            context.Users.AddRange(seasonPlayer, dropInPlayer);
            context.Payments.AddRange(cardCompleted, interacSeason, creditCovered, monthBoundary, cardPending, interacPending,
                refundedCard, refundedCredit, refundedManual, failed, afterRange, seasonAfterRange, beforeRange);
            await context.SaveChangesAsync();
        }

        static bool Is(MoneyCountDto value, decimal amount, int count) => value.Amount == amount && value.Count == count;

        var from = Local(2004, 10, 1);
        var to = Local(2004, 11, 30, 23, 59, 59);
        TreasurerReportDto range;
        await using (var context = db())
            range = await ServiceFor(context).GetReportAsync(new TreasurerReportQuery { From = from, To = to });
        var all = range.Overall;

        assert(Is(all.Collected, 115m, 4) && Is(all.Outstanding, 110m, 2),
            "treasurer report: collected counts completed payments and outstanding counts pending ones, inside the range only");
        assert(Is(all.CollectedByMethod.Interac, 105m, 2) && Is(all.CollectedByMethod.Card, 10m, 1) && Is(all.CollectedByMethod.Other, 0m, 1)
            && Is(all.OutstandingByMethod.Card, 10m, 1) && Is(all.OutstandingByMethod.Interac, 100m, 1) && Is(all.OutstandingByMethod.Other, 0m, 0),
            "treasurer report: cs_ references are card, |INTERAC: references are Interac, everything else is other/manual");
        assert(Is(all.CollectedByPlan.DropIn, 25m, 3) && Is(all.CollectedByPlan.Season, 90m, 1),
            "treasurer report: collected revenue splits into drop-in and season");
        assert(Is(all.PromoCost, 2m, 1) && Is(all.CreditApplied, 20m, 2),
            "treasurer report: promo cost and credit applied come from completed payments only");
        assert(Is(all.Received, 192m, 7) && Is(all.Refunds.Total, 77m, 3) && Is(all.Refunds.Card, 20m, 1) && Is(all.Refunds.AccountCredit, 50m, 1)
            && Is(all.Refunds.Manual, 7m, 1) && Is(all.Refunds.Unrecorded, 0m, 0) && Is(all.Refunds.MoneyReturned, 27m, 2) && all.NetCollected == 165m,
            "treasurer report: refunds split by method, and net collected subtracts only card and manual refunds from money received");

        var october = range.ByMonth.FirstOrDefault(m => m.Label == "2004-10");
        var november = range.ByMonth.FirstOrDefault(m => m.Label == "2004-11");
        assert(range.ByMonth.Select(m => m.Label).SequenceEqual(new[] { "2004-10", "2004-11" }) && october != null && november != null
            && Is(october.Totals.Collected, 115m, 3) && october.Totals.Received.Amount == 185m && Is(october.Totals.Refunds.AccountCredit, 50m, 1) && october.Totals.NetCollected == 185m
            && Is(november.Totals.Collected, 0m, 1) && Is(november.Totals.Outstanding, 110m, 2) && Is(november.Totals.Refunds.MoneyReturned, 27m, 2) && november.Totals.NetCollected == -20m,
            "treasurer report: months follow Toronto time (a late 31 October payment is October) and refunds fall in the month they were made");
        assert(october!.StartUtc == from && october.StartUtc.Kind == DateTimeKind.Utc && october.EndUtc == november!.StartUtc
            && range.Scope.From?.Kind == DateTimeKind.Utc && range.Scope.Kind == TreasurerReportScopeDto.RangeKind,
            "treasurer report: month boundaries and the scope are UTC instants");
        assert(range.BySeason.Count == 2 && range.BySeason[0].SeasonId == season.Id && range.BySeason[0].SeasonName == season.Name
            && Is(range.BySeason[0].Totals.Collected, 90m, 1) && range.BySeason[0].Totals.NetCollected == 140m
            && range.BySeason[1].SeasonId == null && Is(range.BySeason[1].Totals.Collected, 25m, 3) && range.BySeason[1].Totals.NetCollected == 25m,
            "treasurer report: season grouping lists the season, then drop-ins with no season");

        TreasurerReportDto bySeason;
        await using (var context = db())
            bySeason = await ServiceFor(context).GetReportAsync(new TreasurerReportQuery { SeasonId = season.Id });
        assert(bySeason.Scope.Kind == TreasurerReportScopeDto.SeasonKind && bySeason.Scope.SeasonName == season.Name
            && Is(bySeason.Overall.Collected, 190m, 2) && Is(bySeason.Overall.Outstanding, 100m, 1) && bySeason.Overall.NetCollected == 240m
            && Is(bySeason.Overall.Refunds.AccountCredit, 50m, 1) && Is(bySeason.Overall.CollectedByPlan.DropIn, 0m, 0)
            && bySeason.BySeason.Count == 1 && bySeason.ByMonth.Select(m => m.Label).SequenceEqual(new[] { "2004-10", "2004-11", "2004-12" }),
            "treasurer report: a season report covers every payment of that season, whatever its date");

        var adminId = Guid.NewGuid();
        TreasurerReportExport export;
        await using (var context = db())
        {
            assert(!await context.AuditLogs.AnyAsync(a => a.EntityType == TreasurerReportService.AuditEntityType),
                "treasurer report: viewing the report writes no audit entry");
            export = await ServiceFor(context).ExportCsvAsync(new TreasurerReportQuery { From = from, To = to }, adminId, "Treasurer QA");
        }
        var hasBom = export.Content.Length > 3 && export.Content[0] == 0xEF && export.Content[1] == 0xBB && export.Content[2] == 0xBF;
        var text = new UTF8Encoding(false).GetString(export.Content, 3, export.Content.Length - 3);
        var lines = text.Split("\r\n");
        var detailStart = Array.IndexOf(lines, TreasurerReportCsv.DetailHeader);
        var details = detailStart < 0 ? new List<string>() : lines.Skip(detailStart + 1).Where(l => l.Length > 0).ToList();
        assert(hasBom && export.FileName == "treasurer-report-2004-10-01-to-2004-11-30.csv" && export.RowCount == 9 && details.Count == 9,
            "treasurer CSV: UTF-8 with BOM, named after the Toronto dates, one detail row per reported payment (failed and out-of-range left out)");
        assert(lines.Any(l => l.StartsWith("Overall,All,115.00,4,105.00,2,10.00,1,0.00,1,25.00,3,90.00,1,110.00,2,") && l.EndsWith(",27.00,165.00"))
            && lines.Any(l => l.StartsWith("Month,2004-11,0.00,1,") && l.EndsWith(",-20.00"))
            && lines.Any(l => l.StartsWith($"Season,Automne Été {tag},90.00,1,")),
            "treasurer CSV: the summary carries the overall, monthly and season numbers");
        assert(details.Any(l => l == $"2004-10-05 11:00,'=cmd Formula,tr-formula-{tag}@example.test,Drop-in,,Completed,Card,10.00,2.00,0.00,,,cs_tr{tag}_1")
            && details.Any(l => l == $"2004-10-21 11:00,Élodie Trésorière,tr-elodie-{tag}@example.test,Season,Automne Été {tag},Refunded,Interac,50.00,0.00,0.00,Account credit,2004-10-25 11:00,SEASON-TR{tag}8|INTERAC:BANK-{tag}-8")
            && details.Any(l => l.StartsWith("2004-10-31 22:30,") && l.Contains(",Interac,15.00,"))
            && !text.Contains(",=cmd"),
            "treasurer CSV: detail rows show Toronto dates, method, amounts and refund details, keep accents, and neutralise formulas");

        await using (var context = db())
        {
            var entries = await context.AuditLogs.AsNoTracking().Where(a => a.EntityType == TreasurerReportService.AuditEntityType).ToListAsync();
            assert(entries.Count == 1 && entries[0].Action == "Exported" && entries[0].UserId == adminId && entries[0].UserName == "Treasurer QA"
                && entries[0].Details!.Contains("2004-10-01 00:00 to 2004-11-30 23:59") && entries[0].Details!.Contains("9 payment row(s)"),
                "treasurer CSV: each export is audited with the range and row count");
            var seasonExport = await ServiceFor(context).ExportCsvAsync(new TreasurerReportQuery { SeasonId = season.Id }, adminId, "Treasurer QA");
            var seasonEntry = await context.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.EntityType == TreasurerReportService.AuditEntityType && a.EntityId == season.Id);
            assert(seasonExport.FileName == $"treasurer-report-season-automne-ete-{tag}.csv" && seasonExport.RowCount == 4
                && seasonEntry != null && seasonEntry.Details!.Contains(season.Name) && seasonEntry.Details.Contains("4 payment row(s)"),
                "treasurer CSV: a season export names the season in the file and the audit entry");
        }

        async Task<Exception?> Refusal(TreasurerReportQuery query)
        {
            try { await using var context = db(); await ServiceFor(context).GetReportAsync(query); return null; }
            catch (Exception ex) { return ex; }
        }
        var reversed = await Refusal(new TreasurerReportQuery { From = to, To = from });
        var tooLong = await Refusal(new TreasurerReportQuery { From = from, To = from.AddYears(3).AddDays(1) });
        var threeYears = await Refusal(new TreasurerReportQuery { From = from, To = from.AddYears(3) });
        var unknownSeason = await Refusal(new TreasurerReportQuery { SeasonId = Guid.NewGuid() });
        var noScope = await Refusal(new TreasurerReportQuery { From = from });
        var bothScopes = await Refusal(new TreasurerReportQuery { From = from, To = to, SeasonId = season.Id });
        int auditCountAfterRefusedExport;
        try { await using var context = db(); await ServiceFor(context).ExportCsvAsync(new TreasurerReportQuery { From = to, To = from }, adminId, "Treasurer QA"); }
        catch (ValidationException) { }
        await using (var context = db())
            auditCountAfterRefusedExport = await context.AuditLogs.CountAsync(a => a.EntityType == TreasurerReportService.AuditEntityType);
        assert(reversed is ValidationException { Message: "The start date must be on or before the end date." }
            && tooLong is ValidationException && threeYears == null && unknownSeason is NotFoundException
            && noScope is ValidationException && bothScopes is ValidationException && auditCountAfterRefusedExport == 2,
            "treasurer report: refuses a reversed range, a range over 3 years, an unknown season, or a missing or double scope, and a refused export is not audited");
    }
}
