using System.Globalization;
using System.Text;
using SaintHenriBasketball.Application.DTOs.TreasurerReport;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Helpers;

/// <summary>
/// Writes the treasurer report as CSV: a scope header, a summary table (overall, each month, each season) and a
/// payment-level detail table. UTF-8 with a BOM so Excel reads accents; CRLF line endings; amounts use a dot and two
/// decimals. Text cells that start with = + - @ get a leading apostrophe so a spreadsheet won't run them as formulas.
/// </summary>
public static class TreasurerReportCsv
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public const string DetailHeader =
        "Date (America/Toronto),Player,Email,Plan,Season,Status,Method,Amount,Discount,Credit applied,Refund method,Refund date (America/Toronto),Reference";

    private static readonly (string Header, Func<TreasurerTotalsDto, string> Value)[] SummaryColumns =
    {
        ("Collected", t => Money(t.Collected.Amount)),
        ("Collected count", t => Count(t.Collected.Count)),
        ("Interac collected", t => Money(t.CollectedByMethod.Interac.Amount)),
        ("Interac collected count", t => Count(t.CollectedByMethod.Interac.Count)),
        ("Card collected", t => Money(t.CollectedByMethod.Card.Amount)),
        ("Card collected count", t => Count(t.CollectedByMethod.Card.Count)),
        ("Other/manual collected", t => Money(t.CollectedByMethod.Other.Amount)),
        ("Other/manual collected count", t => Count(t.CollectedByMethod.Other.Count)),
        ("Drop-in collected", t => Money(t.CollectedByPlan.DropIn.Amount)),
        ("Drop-in collected count", t => Count(t.CollectedByPlan.DropIn.Count)),
        ("Season collected", t => Money(t.CollectedByPlan.Season.Amount)),
        ("Season collected count", t => Count(t.CollectedByPlan.Season.Count)),
        ("Outstanding", t => Money(t.Outstanding.Amount)),
        ("Outstanding count", t => Count(t.Outstanding.Count)),
        ("Interac outstanding", t => Money(t.OutstandingByMethod.Interac.Amount)),
        ("Interac outstanding count", t => Count(t.OutstandingByMethod.Interac.Count)),
        ("Card outstanding", t => Money(t.OutstandingByMethod.Card.Amount)),
        ("Card outstanding count", t => Count(t.OutstandingByMethod.Card.Count)),
        ("Other/manual outstanding", t => Money(t.OutstandingByMethod.Other.Amount)),
        ("Other/manual outstanding count", t => Count(t.OutstandingByMethod.Other.Count)),
        ("Promo cost", t => Money(t.PromoCost.Amount)),
        ("Discounted payments", t => Count(t.PromoCost.Count)),
        ("Credit applied", t => Money(t.CreditApplied.Amount)),
        ("Payments using credit", t => Count(t.CreditApplied.Count)),
        ("Received", t => Money(t.Received.Amount)),
        ("Received count", t => Count(t.Received.Count)),
        ("Refunds", t => Money(t.Refunds.Total.Amount)),
        ("Refund count", t => Count(t.Refunds.Total.Count)),
        ("Card refunds", t => Money(t.Refunds.Card.Amount)),
        ("Card refund count", t => Count(t.Refunds.Card.Count)),
        ("Account credit refunds", t => Money(t.Refunds.AccountCredit.Amount)),
        ("Account credit refund count", t => Count(t.Refunds.AccountCredit.Count)),
        ("Manual refunds", t => Money(t.Refunds.Manual.Amount)),
        ("Manual refund count", t => Count(t.Refunds.Manual.Count)),
        ("Unrecorded refunds", t => Money(t.Refunds.Unrecorded.Amount)),
        ("Unrecorded refund count", t => Count(t.Refunds.Unrecorded.Count)),
        ("Money returned", t => Money(t.Refunds.MoneyReturned.Amount)),
        ("Net collected", t => Money(t.NetCollected)),
    };

    public static byte[] Build(TreasurerReportDto report, IReadOnlyList<TreasurerPaymentRow> rows, TimeZoneInfo zone)
    {
        var csv = new StringBuilder();
        Line(csv, "Treasurer report");
        if (report.Scope.Kind == TreasurerReportScopeDto.SeasonKind)
        {
            Line(csv, "Scope", "Season");
            Line(csv, "Season", Text(report.Scope.SeasonName));
        }
        else
        {
            Line(csv, "Scope", "Date range");
            Line(csv, $"From ({report.Scope.TimeZone})", LocalText(report.Scope.From, zone));
            Line(csv, $"To ({report.Scope.TimeZone})", LocalText(report.Scope.To, zone));
        }
        Line(csv, "Generated (UTC)", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", Inv));
        Line(csv, "Payments listed", Count(rows.Count));
        Line(csv, "Net collected", "Received (completed and later-refunded payments) minus card, manual and unrecorded refunds; account-credit refunds stay with the club");
        csv.Append("\r\n");

        Line(csv, "Summary");
        Line(csv, new[] { "Group type", "Group" }.Concat(SummaryColumns.Select(c => c.Header)).ToArray());
        SummaryRow(csv, "Overall", "All", report.Overall);
        foreach (var month in report.ByMonth) SummaryRow(csv, "Month", month.Label, month.Totals);
        foreach (var season in report.BySeason) SummaryRow(csv, "Season", Text(season.SeasonName), season.Totals);
        csv.Append("\r\n");

        Line(csv, "Payments");
        csv.Append(DetailHeader).Append("\r\n");
        foreach (var row in rows)
        {
            Line(csv,
                LocalText(row.PaymentDate, zone),
                Text($"{row.FirstName} {row.LastName}".Trim()),
                Text(row.Email),
                row.Plan == PaymentPlan.Season ? "Season" : "Drop-in",
                Text(row.SeasonName),
                row.Status.ToString(),
                MethodLabel(row.Method),
                Money(row.Amount),
                Money(row.DiscountAmount),
                Money(row.CreditApplied),
                RefundLabel(row),
                LocalText(row.RefundedOn, zone),
                Text(row.Reference));
        }

        var text = csv.ToString();
        var preamble = Utf8WithBom.GetPreamble();
        var bytes = new byte[preamble.Length + Utf8WithBom.GetByteCount(text)];
        preamble.CopyTo(bytes, 0);
        Utf8WithBom.GetBytes(text, 0, text.Length, bytes, preamble.Length);
        return bytes;
    }

    /// Lower-case ASCII words joined by dashes, for file names; falls back to the id when nothing is left.
    public static string Slug(string? name, Guid fallbackId)
    {
        var slug = new StringBuilder();
        foreach (var c in (name ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c < 128 && char.IsLetterOrDigit(c)) slug.Append(char.ToLowerInvariant(c));
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        var result = slug.ToString().Trim('-');
        if (result.Length > 60) result = result[..60].Trim('-');
        return result.Length == 0 ? fallbackId.ToString("N")[..8] : result;
    }

    public static string MethodLabel(TreasurerPaymentMethod method) => method switch
    {
        TreasurerPaymentMethod.Interac => "Interac",
        TreasurerPaymentMethod.Card => "Card",
        _ => "Other/manual",
    };

    private static string RefundLabel(TreasurerPaymentRow row) => row.RefundMethod switch
    {
        RefundMethod.Card => "Card",
        RefundMethod.AccountCredit => "Account credit",
        RefundMethod.Manual => "Manual",
        _ => row.Status == PaymentStatus.Refunded ? "Unrecorded" : "",
    };

    private static void SummaryRow(StringBuilder csv, string groupType, string group, TreasurerTotalsDto totals) =>
        Line(csv, new[] { groupType, group }.Concat(SummaryColumns.Select(c => c.Value(totals))).ToArray());

    private static string Money(decimal amount) => amount.ToString("0.00", Inv);
    private static string Count(int count) => count.ToString(Inv);

    private static string LocalText(DateTime? utc, TimeZoneInfo zone) => utc is DateTime value
        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), zone).ToString("yyyy-MM-dd HH:mm", Inv)
        : "";

    /// Guards free text (names, emails, references) against spreadsheet formula injection.
    private static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    private static void Line(StringBuilder csv, params string[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) csv.Append(',');
            var cell = cells[i];
            if (cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                csv.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
            else
                csv.Append(cell);
        }
        csv.Append("\r\n");
    }
}
