using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SaintHenriBasketball.Application.DTOs.TaxReceipts;

namespace SaintHenriBasketball.Application.Helpers;

public class TaxReceiptPdfGenerator
{
    private readonly string _logoPath;

    public TaxReceiptPdfGenerator(IWebHostEnvironment webHostEnvironment)
    {
        var root = string.IsNullOrEmpty(webHostEnvironment?.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : webHostEnvironment.WebRootPath;
        _logoPath = Path.Combine(root, "images", "logo.png");
    }

    public byte[] Generate(TaxReceiptDto receipt, string language = "fr")
    {
        var culture = language == "fr" ? new CultureInfo("fr-CA") : new CultureInfo("en-CA");
        var l = (string en, string fr) => language == "fr" ? fr : en;

        using var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
        var h2Font = new XFont("Arial", 13, XFontStyle.Bold);
        var normalFont = new XFont("Arial", 11, XFontStyle.Regular);
        var smallFont = new XFont("Arial", 9, XFontStyle.Regular);

        if (File.Exists(_logoPath))
        {
            using var logo = XImage.FromFile(_logoPath);
            gfx.DrawImage(logo, 50, 50, 80, 80);
        }

        gfx.DrawString("SAINT-HENRI BASKETBALL", titleFont, XBrushes.Black, new XRect(150, 55, page.Width, 30), XStringFormats.TopLeft);
        gfx.DrawString("717 Saint-Ferdinand Street, Montreal, QC H4C 3L7",
            normalFont, XBrushes.Black, new XRect(150, 85, page.Width, 20), XStringFormats.TopLeft);

        var yPos = 160;
        gfx.DrawString(l($"ANNUAL PAYMENT SUMMARY — {receipt.Year}", $"SOMMAIRE ANNUEL DES PAIEMENTS — {receipt.Year}"),
            h2Font, XBrushes.Black, 50, yPos);

        yPos += 30;
        gfx.DrawString(l("Issued to:", "Remis à :"), h2Font, XBrushes.Black, 50, yPos);
        yPos += 20;
        gfx.DrawString(receipt.UserName, normalFont, XBrushes.Black, 50, yPos);
        if (!string.IsNullOrEmpty(receipt.UserEmail))
        {
            yPos += 16;
            gfx.DrawString(receipt.UserEmail, normalFont, XBrushes.Gray, 50, yPos);
        }

        yPos += 30;
        gfx.DrawString(l("Completed payments", "Paiements complétés"), h2Font, XBrushes.Black, 50, yPos);
        yPos += 25;

        DrawRow(gfx, h2Font, 50, yPos,
            l("Date", "Date"),
            l("Reference", "Référence"),
            l("Plan", "Forfait"),
            l("Amount", "Montant"),
            header: true);
        yPos += 22;

        foreach (var line in receipt.Lines)
        {
            DrawRow(gfx, normalFont, 50, yPos,
                line.PaymentDate.ToString("yyyy-MM-dd", culture),
                line.Reference ?? "—",
                line.PlanLabel,
                $"${line.Amount:F2}");
            yPos += 20;
        }

        yPos += 15;
        gfx.DrawString($"{l("Total", "Total")}: ${receipt.TotalAmount:F2} ({receipt.Lines.Count} {l("payment(s)", "paiement(s)")})",
            h2Font, XBrushes.Black, new XRect(50, yPos, page.Width - 100, 25), XStringFormats.TopRight);

        yPos += 50;
        gfx.DrawString(
            l("This summary is provided for personal record-keeping. Saint-Henri Basketball is not a registered charity; this is not an official tax-deductible receipt.",
              "Ce sommaire est fourni à titre de référence personnelle. Saint-Henri Basketball n'est pas un organisme de bienfaisance enregistré; ceci n'est pas un reçu officiel de déduction fiscale."),
            smallFont, XBrushes.Gray, new XRect(50, yPos, page.Width - 100, 60), XStringFormats.TopLeft);

        var footer = $"{l("Generated", "Généré")} {DateTime.UtcNow.ToString("MMMM dd, yyyy", culture)}";
        gfx.DrawString(footer, smallFont, XBrushes.Gray,
            new XRect(0, page.Height - 40, page.Width, 20), XStringFormats.Center);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void DrawRow(XGraphics gfx, XFont font, double x, double y, string c1, string c2, string c3, string c4, bool header = false)
    {
        var widths = new[] { 90.0, 200.0, 100.0, 100.0 };
        if (header)
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 240, 240)), x, y, widths.Sum(), 20);

        var pen = new XPen(XColors.Gray, 0.3);
        var offset = x;
        var values = new[] { c1, c2, c3, c4 };
        var formats = new[] { XStringFormats.CenterLeft, XStringFormats.CenterLeft, XStringFormats.CenterLeft, XStringFormats.CenterRight };
        for (var i = 0; i < widths.Length; i++)
        {
            gfx.DrawRectangle(pen, offset, y, widths[i], 20);
            gfx.DrawString(values[i], font, XBrushes.Black,
                new XRect(offset + 5, y, widths[i] - 10, 20), formats[i]);
            offset += widths[i];
        }
    }
}
