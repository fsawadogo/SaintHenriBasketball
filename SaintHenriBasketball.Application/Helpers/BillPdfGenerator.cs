using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SaintHenriBasketball.Application.DTOs.Email;

namespace SaintHenriBasketball.Application.Helpers;

public class BillPdfGenerator
{
    private readonly string _logoPath;
    private readonly Dictionary<string, Dictionary<string, string?>> _translations = new()
    {
        { "BILL_TO", new() { { "en", "BILL TO:" }, { "fr", "FACTURER À:" } } },
        { "PAYMENT_DETAILS", new() { { "en", "PAYMENT DETAILS" }, { "fr", "DÉTAILS DU PAIEMENT" } } },
        { "DESCRIPTION", new() { { "en", "Description" }, { "fr", "Description" } } },
        { "AMOUNT", new() { { "en", "Amount" }, { "fr", "Montant" } } },
        { "TOTAL_AMOUNT", new() { { "en", "Total Amount" }, { "fr", "Montant total" } } },
        { "PAYMENT_INSTRUCTIONS", new() { { "en", "Payment Instructions" }, { "fr", "Instructions de paiement" } } },
        { "SEND_PAYMENT", new() { { "en", "Please send payment via Interac e-Transfer to:" }, { "fr", "Veuillez envoyer le paiement par virement Interac à:" } } },
        { "BILL_GENERATED", new() { { "en", "Bill generated on" }, { "fr", "Facture générée le" } } }
    };

    public BillPdfGenerator(IWebHostEnvironment webHostEnvironment)
    {
        if (string.IsNullOrEmpty(webHostEnvironment?.WebRootPath))
        {
            _logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
        }
        else
        {
            _logoPath = Path.Combine(webHostEnvironment.WebRootPath, "images", "logo.png");
        }

        // Verify the logo exists
        if (!File.Exists(_logoPath))
        {
            throw new FileNotFoundException($"Logo file not found at path: {_logoPath}");
        }
    }
    
    public byte[] GenerateBill(BillDetails details, string language = "en")
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        
        var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 12, XFontStyle.Bold);
        var normalFont = new XFont("Arial", 11, XFontStyle.Regular);
        
        // Logo and header
        using var logo = XImage.FromFile(_logoPath);
        gfx.DrawImage(logo, 50, 50, 100, 100);
        gfx.DrawString("SAINT-HENRI BASKETBALL", titleFont, XBrushes.Black, new XRect(170, 50, page.Width, 30), XStringFormats.TopLeft);
        gfx.DrawString("717 Saint-Ferdinand Street", normalFont, XBrushes.Black, new XRect(170, 80, page.Width, 20), XStringFormats.TopLeft);
        gfx.DrawString("Montreal, QC H4C 3L7", normalFont, XBrushes.Black, new XRect(170, 100, page.Width, 20), XStringFormats.TopLeft);
        
        // Bill details
        var yPos = 180;
        gfx.DrawString(GetTranslation("BILL_TO", language), headerFont, XBrushes.Black, 50, yPos);
        yPos += 25;
        gfx.DrawString($"{details.Name}", normalFont, XBrushes.Black, 50, yPos);
        yPos += 20;
        gfx.DrawString($"Email: {details.Email}", normalFont, XBrushes.Black, 50, yPos);
        
        // Payment table
        yPos += 40;
        gfx.DrawString(GetTranslation("PAYMENT_DETAILS", language), headerFont, XBrushes.Black, 50, yPos);
        yPos += 25;
        
        var tableTop = yPos;
        var lineHeight = 25;
        
        DrawTableRow(gfx, headerFont, 50, tableTop, 
            GetTranslation("DESCRIPTION", language), 
            GetTranslation("AMOUNT", language), true);
        
        tableTop += lineHeight;
        DrawTableRow(gfx, normalFont, 50, tableTop, details.Description, $"${details.Amount:F2}");
        
        // Total
        yPos = tableTop + lineHeight + 20;
        gfx.DrawString($"{GetTranslation("TOTAL_AMOUNT", language)}: ${details.Amount:F2}", 
            headerFont, XBrushes.Black, 
            new XRect(50, yPos, page.Width - 100, 30), XStringFormats.TopRight);
        
        // Payment instructions
        yPos += 60;
        gfx.DrawString($"{GetTranslation("PAYMENT_INSTRUCTIONS", language)}:", headerFont, XBrushes.Black, 50, yPos);
        yPos += 25;
        gfx.DrawString(GetTranslation("SEND_PAYMENT", language), normalFont, XBrushes.Black, 50, yPos);
        yPos += 20;
        gfx.DrawString("pay@sainthenribasketball.com", normalFont, XBrushes.Black, 50, yPos);
        
        // Footer
        var footerText = $"{GetTranslation("BILL_GENERATED", language)} {DateTime.Now.ToString("MMMM dd, yyyy", GetCulture(language))}";
        gfx.DrawString(footerText, normalFont, XBrushes.Gray, 
            new XRect(0, page.Height - 50, page.Width, 30), XStringFormats.Center);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private string? GetTranslation(string key, string language) => 
        _translations[key][language];

    private CultureInfo GetCulture(string language) =>
        language == "fr" ? new CultureInfo("fr-CA") : new CultureInfo("en-CA");

    private void DrawTableRow(XGraphics gfx, XFont font, double x, double y, string? col1, string? col2, bool isHeader = false)
    {
        var pen = new XPen(XColors.Gray, 0.5);
   
        // Cell dimensions
        var cellPadding = 5;
        var col1Width = 350;
        var col2Width = 150;
        var rowHeight = 25;
   
        // Draw header background if header row
        if (isHeader)
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 240, 240)), 
                x, y, col1Width + col2Width, rowHeight);
        }
   
        // Draw cell borders
        gfx.DrawRectangle(pen, x, y, col1Width, rowHeight);
        gfx.DrawRectangle(pen, x + col1Width, y, col2Width, rowHeight);
   
        // Draw text in cells
        gfx.DrawString(col1, font, XBrushes.Black, 
            new XRect(x + cellPadding, y, col1Width - 2 * cellPadding, rowHeight), 
            XStringFormats.CenterLeft);
       
        gfx.DrawString(col2, font, XBrushes.Black, 
            new XRect(x + col1Width + cellPadding, y, col2Width - 2 * cellPadding, rowHeight), 
            XStringFormats.CenterRight);
    }
}