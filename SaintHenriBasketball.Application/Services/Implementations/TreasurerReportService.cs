using System.Globalization;
using SaintHenriBasketball.Application.DTOs.TreasurerReport;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// <summary>
/// Treasurer report. The money rules:
/// <list type="bullet">
/// <item>Collected = Completed payments; outstanding = Pending payments. Both are dated by PaymentDate, which is set when
/// a payment completes (for pending payments it is still the creation date). Failed payments are left out.</item>
/// <item>Method: card if the reference starts with "cs_" (Stripe Checkout Session id), Interac if it contains
/// "|INTERAC:" (bank confirmation number), otherwise other/manual. See TreasurerPaymentMethod.</item>
/// <item>Received = Completed + Refunded payments by PaymentDate. A refunded payment was collected first, and its status
/// no longer says Completed, so subtracting refunds from Collected would count the refund twice.</item>
/// <item>Refunds are dated by RefundedOn. Card and Manual refunds (and refunds with no method recorded) returned money;
/// NetCollected = Received - those. Account-credit refunds are reported but not subtracted: the money stays in the
/// club's account and becomes a credit the player owes nothing for. When that credit is later spent it shows up
/// as CreditApplied on a lower-priced payment, so it is never counted as cash twice.</item>
/// </list>
/// </summary>
public class TreasurerReportService : ITreasurerReportService
{
    public const string DateRangeMessage = "The start date must be on or before the end date.";
    public const string RangeTooLongMessage = "The date range can't be longer than 3 years.";
    public const string ScopeRequiredMessage = "Choose a date range (from and to) or a season.";
    public const string ScopeConflictMessage = "Choose a date range or a season, not both.";
    public const int MaxRangeYears = 3;
    public const string TimeZoneName = "America/Toronto";
    public const string AuditAction = "Exported";
    public const string AuditEntityType = "TreasurerReport";
    public const string NoSeasonName = "No season";

    public static readonly TimeZoneInfo TorontoTimeZone = ResolveTimeZone();

    private readonly ITreasurerReportRepository _repository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IAuditLogService _auditLogService;

    public TreasurerReportService(ITreasurerReportRepository repository, ISeasonRepository seasonRepository, IAuditLogService auditLogService)
    {
        _repository = repository;
        _seasonRepository = seasonRepository;
        _auditLogService = auditLogService;
    }

    private sealed record Scope(TreasurerReportFilter Filter, DateTime? From, DateTime? To, Season? Season);

    public async Task<TreasurerReportDto> GetReportAsync(TreasurerReportQuery query) =>
        await BuildAsync(await ResolveScopeAsync(query));

    public async Task<TreasurerReportExport> ExportCsvAsync(TreasurerReportQuery query, Guid? adminId, string adminName)
    {
        var scope = await ResolveScopeAsync(query);
        var report = await BuildAsync(scope);
        var rows = await _repository.GetPaymentRowsAsync(scope.Filter);
        var content = TreasurerReportCsv.Build(report, rows, TorontoTimeZone);

        string fileName, details;
        if (scope.Season is { } season)
        {
            fileName = $"treasurer-report-season-{TreasurerReportCsv.Slug(season.Name, season.Id)}.csv";
            details = FormattableString.Invariant($"Treasurer report CSV for season \"{season.Name}\" ({season.Id}): {rows.Count} payment row(s)");
        }
        else
        {
            var fromLocal = ToLocal(scope.From!.Value);
            var toLocal = ToLocal(scope.To!.Value);
            fileName = FormattableString.Invariant($"treasurer-report-{fromLocal:yyyy-MM-dd}-to-{toLocal:yyyy-MM-dd}.csv");
            details = FormattableString.Invariant(
                $"Treasurer report CSV for {fromLocal:yyyy-MM-dd HH:mm} to {toLocal:yyyy-MM-dd HH:mm} ({TimeZoneName}): {rows.Count} payment row(s)");
        }

        // Written before the file is returned: an export that can't be audited isn't handed out.
        await _auditLogService.LogAsync(AuditAction, AuditEntityType, scope.Season?.Id, details, adminId, adminName);
        return new TreasurerReportExport(content, fileName, rows.Count);
    }

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TorontoTimeZone);

    private static DateTime LocalMonthStartUtc(int year, int month) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified), TorontoTimeZone);

    private async Task<Scope> ResolveScopeAsync(TreasurerReportQuery query)
    {
        var from = AsUtc(query.From);
        var to = AsUtc(query.To);

        if (query.SeasonId is Guid seasonId)
        {
            if (from != null || to != null)
                throw new ValidationException(ScopeConflictMessage);
            var season = await _seasonRepository.GetByIdAsync(seasonId)
                ?? throw new NotFoundException($"Season {seasonId} not found");
            return new Scope(new TreasurerReportFilter(null, null, seasonId), null, null, season);
        }

        if (from is not DateTime start || to is not DateTime end)
            throw new ValidationException(ScopeRequiredMessage);
        if (start > end)
            throw new ValidationException(DateRangeMessage);
        if (start.Year > DateTime.MaxValue.Year - MaxRangeYears || end > start.AddYears(MaxRangeYears))
            throw new ValidationException(RangeTooLongMessage);
        return new Scope(new TreasurerReportFilter(start, end, null), start, end, null);
    }

    private static DateTime? AsUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        { } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    };

    private async Task<TreasurerReportDto> BuildAsync(Scope scope)
    {
        var receipts = await _repository.GetReceiptBucketsAsync(scope.Filter);
        var refunds = await _repository.GetRefundBucketsAsync(scope.Filter);

        var seasonIds = receipts.Select(b => b.SeasonId).Concat(refunds.Select(b => b.SeasonId)).OfType<Guid>().ToHashSet();
        if (scope.Season != null) seasonIds.Add(scope.Season.Id);
        var seasons = (await _repository.GetSeasonsAsync(seasonIds)).ToDictionary(s => s.Id);

        var overall = new TotalsBuilder();
        var months = new SortedDictionary<(int Year, int Month), TotalsBuilder>();
        var bySeason = new Dictionary<Guid, TotalsBuilder>();
        TotalsBuilder? noSeason = null;
        if (scope.Season != null) bySeason[scope.Season.Id] = new TotalsBuilder();

        TotalsBuilder MonthOf(DateTime hourUtc)
        {
            var local = ToLocal(hourUtc);
            var key = (local.Year, local.Month);
            if (!months.TryGetValue(key, out var builder)) months[key] = builder = new TotalsBuilder();
            return builder;
        }
        TotalsBuilder SeasonOf(Guid? seasonId)
        {
            if (seasonId is not Guid id) return noSeason ??= new TotalsBuilder();
            if (!bySeason.TryGetValue(id, out var builder)) bySeason[id] = builder = new TotalsBuilder();
            return builder;
        }

        foreach (var bucket in receipts)
        {
            overall.Add(bucket);
            MonthOf(bucket.HourUtc).Add(bucket);
            SeasonOf(bucket.SeasonId).Add(bucket);
        }
        foreach (var bucket in refunds)
        {
            overall.Add(bucket);
            MonthOf(bucket.HourUtc).Add(bucket);
            SeasonOf(bucket.SeasonId).Add(bucket);
        }

        // Every month in the range (or between the season's first and last activity) gets a row, even an empty one.
        (int Year, int Month)? firstMonth = scope.From is DateTime from ? (ToLocal(from).Year, ToLocal(from).Month) : months.Count > 0 ? months.Keys.First() : null;
        (int Year, int Month)? lastMonth = scope.To is DateTime to ? (ToLocal(to).Year, ToLocal(to).Month) : months.Count > 0 ? months.Keys.Last() : null;
        if (firstMonth is { } first && lastMonth is { } last)
        {
            for (var cursor = new DateTime(first.Year, first.Month, 1); cursor <= new DateTime(last.Year, last.Month, 1); cursor = cursor.AddMonths(1))
                if (!months.ContainsKey((cursor.Year, cursor.Month))) months[(cursor.Year, cursor.Month)] = new TotalsBuilder();
        }

        var seasonRows = bySeason
            .Select(pair => (Info: seasons.GetValueOrDefault(pair.Key), Id: pair.Key, Totals: pair.Value))
            .OrderBy(s => s.Info?.StartDate ?? DateTime.MaxValue)
            .ThenBy(s => s.Info?.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new TreasurerSeasonDto
            {
                SeasonId = s.Id,
                SeasonName = s.Info?.Name ?? (scope.Season?.Id == s.Id ? scope.Season.Name : "Unknown season"),
                Totals = s.Totals.ToDto(),
            })
            .ToList();
        if (noSeason != null)
            seasonRows.Add(new TreasurerSeasonDto { SeasonId = null, SeasonName = NoSeasonName, Totals = noSeason.ToDto() });

        return new TreasurerReportDto
        {
            Scope = new TreasurerReportScopeDto
            {
                Kind = scope.Season != null ? TreasurerReportScopeDto.SeasonKind : TreasurerReportScopeDto.RangeKind,
                From = scope.From,
                To = scope.To,
                SeasonId = scope.Season?.Id,
                SeasonName = scope.Season?.Name,
                TimeZone = TimeZoneName,
            },
            GeneratedAt = DateTime.UtcNow,
            Overall = overall.ToDto(),
            ByMonth = months.Select(pair =>
            {
                var next = new DateTime(pair.Key.Year, pair.Key.Month, 1).AddMonths(1);
                return new TreasurerMonthDto
                {
                    Year = pair.Key.Year,
                    Month = pair.Key.Month,
                    Label = string.Create(CultureInfo.InvariantCulture, $"{pair.Key.Year:0000}-{pair.Key.Month:00}"),
                    StartUtc = LocalMonthStartUtc(pair.Key.Year, pair.Key.Month),
                    EndUtc = LocalMonthStartUtc(next.Year, next.Month),
                    Totals = pair.Value.ToDto(),
                };
            }).ToList(),
            BySeason = seasonRows,
        };
    }

    private sealed class Tally
    {
        public decimal Amount;
        public int Count;
        public void Add(decimal amount, int count) { Amount += amount; Count += count; }
        public MoneyCountDto ToDto() => new() { Amount = Amount, Count = Count };
    }

    private sealed class TotalsBuilder
    {
        private readonly Tally _collected = new(), _collectedInterac = new(), _collectedCard = new(), _collectedOther = new();
        private readonly Tally _dropIn = new(), _season = new();
        private readonly Tally _outstanding = new(), _outstandingInterac = new(), _outstandingCard = new(), _outstandingOther = new();
        private readonly Tally _promo = new(), _credit = new(), _received = new();
        private readonly Tally _refunds = new(), _refundCard = new(), _refundCredit = new(), _refundManual = new(), _refundUnrecorded = new();

        public void Add(TreasurerReceiptBucket b)
        {
            switch (b.Status)
            {
                case PaymentStatus.Completed:
                    _collected.Add(b.Amount, b.Count);
                    ByMethod(b.Method, _collectedInterac, _collectedCard, _collectedOther).Add(b.Amount, b.Count);
                    (b.Plan == PaymentPlan.Season ? _season : _dropIn).Add(b.Amount, b.Count);
                    _promo.Add(b.DiscountAmount, b.DiscountedCount);
                    _credit.Add(b.CreditApplied, b.CreditCount);
                    _received.Add(b.Amount, b.Count);
                    break;
                case PaymentStatus.Refunded:
                    _received.Add(b.Amount, b.Count);
                    break;
                case PaymentStatus.Pending:
                    _outstanding.Add(b.Amount, b.Count);
                    ByMethod(b.Method, _outstandingInterac, _outstandingCard, _outstandingOther).Add(b.Amount, b.Count);
                    break;
            }
        }

        public void Add(TreasurerRefundBucket b)
        {
            _refunds.Add(b.Amount, b.Count);
            (b.RefundMethod switch
            {
                RefundMethod.Card => _refundCard,
                RefundMethod.AccountCredit => _refundCredit,
                RefundMethod.Manual => _refundManual,
                _ => _refundUnrecorded,
            }).Add(b.Amount, b.Count);
        }

        private static Tally ByMethod(TreasurerPaymentMethod method, Tally interac, Tally card, Tally other) => method switch
        {
            TreasurerPaymentMethod.Interac => interac,
            TreasurerPaymentMethod.Card => card,
            _ => other,
        };

        public TreasurerTotalsDto ToDto()
        {
            var moneyReturned = new MoneyCountDto
            {
                Amount = _refundCard.Amount + _refundManual.Amount + _refundUnrecorded.Amount,
                Count = _refundCard.Count + _refundManual.Count + _refundUnrecorded.Count,
            };
            return new TreasurerTotalsDto
            {
                Collected = _collected.ToDto(),
                CollectedByMethod = new TreasurerMethodSplitDto { Interac = _collectedInterac.ToDto(), Card = _collectedCard.ToDto(), Other = _collectedOther.ToDto() },
                CollectedByPlan = new TreasurerPlanSplitDto { DropIn = _dropIn.ToDto(), Season = _season.ToDto() },
                Outstanding = _outstanding.ToDto(),
                OutstandingByMethod = new TreasurerMethodSplitDto { Interac = _outstandingInterac.ToDto(), Card = _outstandingCard.ToDto(), Other = _outstandingOther.ToDto() },
                PromoCost = _promo.ToDto(),
                CreditApplied = _credit.ToDto(),
                Received = _received.ToDto(),
                Refunds = new TreasurerRefundsDto
                {
                    Total = _refunds.ToDto(),
                    Card = _refundCard.ToDto(),
                    AccountCredit = _refundCredit.ToDto(),
                    Manual = _refundManual.ToDto(),
                    Unrecorded = _refundUnrecorded.ToDto(),
                    MoneyReturned = moneyReturned,
                },
                NetCollected = _received.Amount - moneyReturned.Amount,
            };
        }
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { TimeZoneName, "Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return SessionTimeHelper.MontrealTimeZone;
    }
}
