using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SaintHenriBasketball.Application.DTOs.Email;

namespace SaintHenriBasketball.Application.Helpers;

/// <summary>
/// The receipt PDF attached to a payment confirmation.
///
/// Drawn rather than templated, so the layout is a column of blocks laid out top-down: a dark band
/// carrying the club mark, the parties, the lines that make up the charge, the amount paid, and how
/// it was paid. The amount is the largest thing on the page because it is what anyone opening a
/// receipt is looking for.
/// </summary>
public class BillPdfGenerator
{
    // The club's colours, matching the app and the emails.
    private static readonly XColor Court = XColor.FromArgb(0x20, 0x38, 0x2c);
    private static readonly XColor Moss = XColor.FromArgb(0x2e, 0x4b, 0x3c);
    private static readonly XColor Gold = XColor.FromArgb(0xec, 0xc3, 0x82);
    private static readonly XColor Ink = XColor.FromArgb(0x15, 0x28, 0x1e);
    private static readonly XColor Sage = XColor.FromArgb(0x63, 0x73, 0x69);
    private static readonly XColor Hairline = XColor.FromArgb(0xe0, 0xe6, 0xe2);
    private static readonly XColor Wash = XColor.FromArgb(0xf2, 0xf6, 0xf2);
    private static readonly XColor OnDark = XColor.FromArgb(0xc5, 0xd9, 0xc8);

    private const double Margin = 52;
    private const double BandHeight = 104;

    /// The club mark, or null when no logo file is available. A missing image is not a reason to
    /// withhold someone's receipt, so the header simply closes up around it.
    private readonly string? _logoPath;

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        { "RECEIPT", new() { { "en", "Receipt" }, { "fr", "Reçu" } } },
        { "BILL_TO", new() { { "en", "Billed to" }, { "fr", "Facturé à" } } },
        { "DETAILS", new() { { "en", "Details" }, { "fr", "Détails" } } },
        { "ISSUED", new() { { "en", "Issued" }, { "fr", "Émis le" } } },
        { "PAID_ON", new() { { "en", "Paid" }, { "fr", "Payé le" } } },
        { "METHOD", new() { { "en", "Method" }, { "fr", "Méthode" } } },
        { "PAYMENT", new() { { "en", "Payment" }, { "fr", "Paiement" } } },
        { "DESCRIPTION", new() { { "en", "Description" }, { "fr", "Description" } } },
        { "AMOUNT", new() { { "en", "Amount" }, { "fr", "Montant" } } },
        { "DISCOUNT", new() { { "en", "Promotional discount" }, { "fr", "Rabais promotionnel" } } },
        { "CREDIT", new() { { "en", "Account credit applied" }, { "fr", "Crédit du compte appliqué" } } },
        { "TOTAL_PAID", new() { { "en", "Amount paid" }, { "fr", "Montant payé" } } },
        { "TOTAL_DUE", new() { { "en", "Amount due" }, { "fr", "Montant dû" } } },
        { "PAID_MARK", new() { { "en", "Paid in full" }, { "fr", "Payé en entier" } } },
        { "HOW_PAID", new() { { "en", "How this was paid" }, { "fr", "Mode de paiement" } } },
        { "HOW_TO_PAY", new() { { "en", "How to pay" }, { "fr", "Comment payer" } } },
        { "PAID_BY", new() { { "en", "Interac e-Transfer to {0}, quoting {1}. Keep this receipt for your records." },
                             { "fr", "Virement Interac à {0}, en inscrivant {1}. Conservez ce reçu." } } },
        { "PLEASE_PAY", new() { { "en", "Send an Interac e-Transfer to {0}, putting {1} in the message so we can match it to you." },
                                { "fr", "Envoyez un virement Interac à {0}, en inscrivant {1} dans le message pour qu'on puisse l'associer à votre compte." } } },
        { "GENERATED", new() { { "en", "Generated {0}" }, { "fr", "Générée le {0}" } } },
    };

    public BillPdfGenerator(IWebHostEnvironment webHostEnvironment)
    {
        var root = string.IsNullOrEmpty(webHostEnvironment?.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : webHostEnvironment.WebRootPath;

        // The club mark first; logo.png is the old orange badge, kept only as a fallback.
        _logoPath = FirstExisting(
            Path.Combine(root, "images", "club-mark.png"),
            Path.Combine(root, "images", "logo.png"));
    }

    private static string? FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists);

    public byte[] GenerateBill(BillDetails details, string language = "fr")
    {
        var culture = GetCulture(language);
        using var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var display = new XFont("Arial", 19, XFontStyle.Bold);
        var clubFont = new XFont("Arial", 15, XFontStyle.Bold);
        var labelFont = new XFont("Arial", 7.5, XFontStyle.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
        var bodyBold = new XFont("Arial", 10, XFontStyle.Bold);
        var smallFont = new XFont("Arial", 8.5, XFontStyle.Regular);
        var totalFont = new XFont("Arial", 24, XFontStyle.Bold);

        var right = page.Width - Margin;
        var contentWidth = right - Margin;

        DrawBand(gfx, page, details, language, clubFont, labelFont, smallFont, bodyBold);

        var y = BandHeight + 34;

        // --- Parties ---
        y = DrawParties(gfx, details, language, culture, labelFont, bodyFont, bodyBold, smallFont, right, y);

        // --- Lines ---
        y += 26;
        gfx.DrawString(T("PAYMENT", language).ToUpperInvariant(), labelFont, new XSolidBrush(Sage), Margin, y);
        y += 14;

        gfx.DrawString(T("DESCRIPTION", language).ToUpperInvariant(), labelFont, new XSolidBrush(Sage), Margin, y);
        gfx.DrawString(T("AMOUNT", language).ToUpperInvariant(), labelFont, new XSolidBrush(Sage),
            new XRect(Margin, y - 9, contentWidth, 12), XStringFormats.TopRight);
        y += 6;
        gfx.DrawLine(new XPen(Court, 1.4), Margin, y, right, y);
        y += 16;

        foreach (var (label, amount) in BuildLines(details, language))
        {
            gfx.DrawString(label, bodyFont, new XSolidBrush(Ink), Margin, y);
            gfx.DrawString(Money(amount, culture), bodyFont, new XSolidBrush(Ink),
                new XRect(Margin, y - 10, contentWidth, 14), XStringFormats.TopRight);
            y += 10;
            gfx.DrawLine(new XPen(Hairline, 0.8), Margin, y, right, y);
            y += 18;
        }

        // --- The figure the reader came for ---
        y += 6;
        var boxHeight = 52.0;
        gfx.DrawRectangle(new XPen(Hairline, 0.8), new XSolidBrush(Wash), Margin, y, contentWidth, boxHeight);
        var paid = details.PaidOn is not null;
        gfx.DrawString(T(paid ? "TOTAL_PAID" : "TOTAL_DUE", language).ToUpperInvariant(), labelFont,
            new XSolidBrush(Moss), Margin + 14, y + 22);
        gfx.DrawString(Money(details.Amount, culture), totalFont, new XSolidBrush(Ink),
            new XRect(Margin, y + 12, contentWidth - 14, 30), XStringFormats.TopRight);
        y += boxHeight + 16;

        if (paid)
        {
            var mark = T("PAID_MARK", language).ToUpperInvariant();
            var markWidth = gfx.MeasureString(mark, labelFont).Width + 18;
            gfx.DrawRectangle(new XPen(Moss, 1), Margin, y, markWidth, 18);
            gfx.DrawString(mark, labelFont, new XSolidBrush(Moss),
                new XRect(Margin, y, markWidth, 18), XStringFormats.Center);
            y += 34;
        }

        // --- How it was paid ---
        y += 8;
        gfx.DrawRectangle(new XSolidBrush(Gold), Margin, y, 3, 44);
        gfx.DrawString(T(paid ? "HOW_PAID" : "HOW_TO_PAY", language).ToUpperInvariant(), labelFont,
            new XSolidBrush(Sage), Margin + 14, y + 10);
        var how = string.Format(
            culture,
            T(paid ? "PAID_BY" : "PLEASE_PAY", language),
            details.PaymentEmail,
            string.IsNullOrWhiteSpace(details.Reference) ? "—" : details.Reference);
        DrawWrapped(gfx, how, smallFont, new XSolidBrush(Ink), Margin + 14, y + 24, contentWidth - 14, 12);

        // --- Footer ---
        var footerY = page.Height - 54;
        gfx.DrawLine(new XPen(Hairline, 0.8), Margin, footerY, right, footerY);
        gfx.DrawString(string.Format(culture, T("GENERATED", language), details.Date.ToString("d MMMM yyyy", culture)),
            smallFont, new XSolidBrush(Sage), Margin, footerY + 16);
        gfx.DrawString("sainthenribasketball.com", smallFont, new XSolidBrush(Sage),
            new XRect(Margin, footerY + 6, contentWidth, 14), XStringFormats.TopRight);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private void DrawBand(XGraphics gfx, PdfPage page, BillDetails details, string language,
        XFont clubFont, XFont labelFont, XFont smallFont, XFont bodyBold)
    {
        gfx.DrawRectangle(new XSolidBrush(Court), 0, 0, page.Width, BandHeight);
        gfx.DrawRectangle(new XSolidBrush(Gold), 0, BandHeight, page.Width, 3);

        var textLeft = Margin;
        if (_logoPath is not null)
        {
            try
            {
                using var logo = XImage.FromFile(_logoPath);
                gfx.DrawImage(logo, Margin, 26, 52, 52);
                textLeft = Margin + 68;
            }
            catch
            {
                // An unreadable image must not cost someone their receipt.
            }
        }

        gfx.DrawString("Saint-Henri Basketball", clubFont, XBrushes.White, textLeft, 48);
        gfx.DrawString(details.Location, smallFont, new XSolidBrush(OnDark), textLeft, 66);

        var right = page.Width - Margin;
        gfx.DrawString(T("RECEIPT", language).ToUpperInvariant(), labelFont, new XSolidBrush(Gold),
            new XRect(0, 38, right, 12), XStringFormats.TopRight);
        gfx.DrawString(string.IsNullOrWhiteSpace(details.Reference) ? "—" : details.Reference,
            bodyBold, XBrushes.White, new XRect(0, 54, right, 16), XStringFormats.TopRight);
    }

    private double DrawParties(XGraphics gfx, BillDetails details, string language, CultureInfo culture,
        XFont labelFont, XFont bodyFont, XFont bodyBold, XFont smallFont, double right, double y)
    {
        gfx.DrawString(T("BILL_TO", language).ToUpperInvariant(), labelFont, new XSolidBrush(Sage), Margin, y);
        gfx.DrawString(T("DETAILS", language).ToUpperInvariant(), labelFont, new XSolidBrush(Sage), right - 190, y);

        var leftY = y + 18;
        gfx.DrawString(details.Name, bodyBold, new XSolidBrush(Ink), Margin, leftY);
        if (!string.IsNullOrWhiteSpace(details.Email))
            gfx.DrawString(details.Email, smallFont, new XSolidBrush(Sage), Margin, leftY + 15);

        var rowY = y + 18;
        void Row(string label, string value)
        {
            gfx.DrawString(label, smallFont, new XSolidBrush(Sage), right - 190, rowY);
            gfx.DrawString(value, smallFont, new XSolidBrush(Ink),
                new XRect(right - 190, rowY - 10, 190, 14), XStringFormats.TopRight);
            rowY += 15;
        }

        Row(T("ISSUED", language), details.Date.ToString("d MMM yyyy", culture));
        if (details.PaidOn is DateTime paidOn) Row(T("PAID_ON", language), paidOn.ToString("d MMM yyyy", culture));
        Row(T("METHOD", language), details.PaymentMethod);

        return Math.Max(leftY + 15, rowY);
    }

    /// <summary>
    /// The rows that make up the charge. A payment reduced by a promo or by account credit used to
    /// show only its final figure, leaving the reader to wonder where the rest went.
    /// </summary>
    private IEnumerable<(string Label, decimal Amount)> BuildLines(BillDetails details, string language)
    {
        var listed = details.OriginalAmount ?? details.Amount;
        yield return (details.Description ?? "—", listed);

        if (details.DiscountAmount > 0) yield return (T("DISCOUNT", language), -details.DiscountAmount);
        if (details.CreditApplied > 0) yield return (T("CREDIT", language), -details.CreditApplied);
    }

    private static void DrawWrapped(XGraphics gfx, string text, XFont font, XBrush brush,
        double x, double y, double width, double lineHeight)
    {
        var words = text.Split(' ');
        var line = string.Empty;

        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (gfx.MeasureString(candidate, font).Width > width && line.Length > 0)
            {
                gfx.DrawString(line, font, brush, x, y);
                y += lineHeight;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0) gfx.DrawString(line, font, brush, x, y);
    }

    private static string Money(decimal amount, CultureInfo culture) =>
        amount < 0 ? $"−{Math.Abs(amount).ToString("C", culture)}" : amount.ToString("C", culture);

    private string T(string key, string language) =>
        _translations[key].TryGetValue(language, out var value) ? value : _translations[key]["en"];

    private static CultureInfo GetCulture(string language) =>
        language == "fr" ? new CultureInfo("fr-CA") : new CultureInfo("en-CA");
}
