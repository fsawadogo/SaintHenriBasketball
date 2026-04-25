using System.Globalization;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Helpers;

public static class EmailTemplateHelper
{
    // Midnight Court email color tokens
    private const string ColorDark = "#0a0e1a";
    private const string ColorSurface = "#111520";
    private const string ColorAccent = "#f97316";
    private const string ColorText = "#1a1a2e";
    private const string ColorTextLight = "#6b7280";
    private const string ColorBg = "#f0f0f0";
    private const string ColorCard = "#ffffff";
    private const string ColorBorder = "#e5e7eb";
    private const string ColorInfoBg = "#f8f9fa";
    private const string ColorSuccess = "#22c55e";
    private const string ColorWarning = "#eab308";
    private const string ColorDanger = "#ef4444";
    private const string LogoUrl = "https://sainthenribasketball.com/logo.png";
    private const string WhatsAppGroupUrl = "https://chat.whatsapp.com/JGAbUroUK9B6feJ6uDlwwD";

    private static readonly CultureInfo FrenchCulture = new("fr-CA");
    private static readonly CultureInfo EnglishCulture = new("en-CA");

    /// <summary>Returns text in the requested language. For Bilingual, shows FR primary + EN secondary.</summary>
    public static string L(string en, string fr, EmailLanguage language) => language switch
    {
        EmailLanguage.English => en,
        EmailLanguage.French => fr,
        EmailLanguage.Bilingual => $"{fr}<br/><span style='color:{ColorTextLight};font-size:13px;'>{en}</span>",
        _ => fr
    };

    /// <summary>Returns subject text (no HTML) in the requested language.</summary>
    public static string LSubject(string en, string fr, EmailLanguage language) => language switch
    {
        EmailLanguage.English => en,
        EmailLanguage.French => fr,
        EmailLanguage.Bilingual => $"{fr} / {en}",
        _ => fr
    };

    /// <summary>Returns the CultureInfo for date formatting based on language.</summary>
    public static CultureInfo GetCulture(EmailLanguage language) => language switch
    {
        EmailLanguage.English => EnglishCulture,
        _ => FrenchCulture
    };

    /// <summary>Builds the full email HTML layout with dark header, white content, signature
    /// + WhatsApp link, and dark footer. Every template flows through here so the
    /// signature and chat link stay in lockstep across the app.</summary>
    public static string BuildEmailLayout(string titleEn, string titleFr, string content, EmailLanguage language = EmailLanguage.French)
    {
        var title = LSubject(titleEn, titleFr, language);
        var lang = language == EmailLanguage.English ? "en" : "fr";
        var signature = L("Team SHB", "Équipe SHB", language);
        var whatsappCta = L("Check the WhatsApp group for more details", "Consulter le groupe WhatsApp pour plus de détails", language);
        var signoff = L("See you on the court,", "À bientôt sur le terrain,", language);

        return $@"<!DOCTYPE html>
<html lang='{lang}'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
</head>
<body style='margin:0;padding:0;background-color:{ColorBg};font-family:Arial,Helvetica,sans-serif;-webkit-font-smoothing:antialiased;'>
    <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background-color:{ColorBg};'>
        <tr>
            <td align='center' style='padding:24px 16px;'>
                <table role='presentation' width='600' cellpadding='0' cellspacing='0' style='max-width:600px;width:100%;'>
                    <!-- Header -->
                    <tr>
                        <td style='background-color:{ColorDark};border-radius:12px 12px 0 0;padding:24px 32px;text-align:center;'>
                            <img src='{LogoUrl}' alt='Saint-Henri Basketball' width='48' height='48' style='display:inline-block;margin-bottom:12px;border-radius:8px;' />
                            <h1 style='margin:0;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:-0.3px;'>{title}</h1>
                        </td>
                    </tr>
                    <!-- Orange accent line -->
                    <tr>
                        <td style='background-color:{ColorAccent};height:3px;font-size:0;line-height:0;'>&nbsp;</td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style='background-color:{ColorCard};padding:32px 32px 8px;'>
                            <div style='color:{ColorText};font-size:15px;line-height:1.6;'>
                                {content}
                            </div>
                        </td>
                    </tr>
                    <!-- Signature + WhatsApp CTA -->
                    <tr>
                        <td style='background-color:{ColorCard};padding:0 32px 28px;'>
                            <div style='border-top:1px solid {ColorBorder};padding-top:20px;color:{ColorText};font-size:14px;line-height:1.6;'>
                                <p style='margin:0;color:{ColorTextLight};font-size:13px;'>{signoff}</p>
                                <p style='margin:4px 0 16px;font-weight:600;color:{ColorText};'>{signature}</p>
                                <a href='{WhatsAppGroupUrl}' style='display:inline-block;background-color:#25D366;color:#ffffff;font-size:13px;font-weight:600;text-decoration:none;padding:10px 18px;border-radius:6px;'>💬 {whatsappCta}</a>
                            </div>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='background-color:{ColorDark};border-radius:0 0 12px 12px;padding:20px 32px;text-align:center;'>
                            <p style='margin:0 0 4px;color:#9ca3af;font-size:12px;'>Saint-Henri Basketball</p>
                            <p style='margin:0 0 4px;color:#6b7280;font-size:11px;'>717 Saint-Ferdinand Street, Montreal, QC H4C 3L7</p>
                            <p style='margin:0;color:#6b7280;font-size:11px;'>(438) 935-8129 &middot; info@sainthenribasketball.com</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    /// <summary>Builds an orange CTA button.</summary>
    public static string BuildButton(string textEn, string textFr, string url, EmailLanguage language = EmailLanguage.French)
    {
        var text = language switch
        {
            EmailLanguage.English => textEn,
            EmailLanguage.French => textFr,
            _ => textFr
        };

        return $@"<div style='text-align:center;margin:24px 0;'>
            <a href='{url}' style='display:inline-block;background-color:{ColorAccent};color:#ffffff;font-size:14px;font-weight:600;text-decoration:none;padding:12px 28px;border-radius:8px;'>{text}</a>
        </div>";
    }

    /// <summary>Builds a secondary (dark) button.</summary>
    public static string BuildSecondaryButton(string text, string url)
    {
        return $@"<div style='text-align:center;margin:16px 0;'>
            <a href='{url}' style='display:inline-block;background-color:{ColorDark};color:#ffffff;font-size:14px;font-weight:600;text-decoration:none;padding:12px 28px;border-radius:8px;'>{text}</a>
        </div>";
    }

    /// <summary>Builds a key-value info box with light gray background.</summary>
    public static string BuildInfoBox(Dictionary<string, string?> fields)
    {
        var rows = string.Join("", fields
            .Where(f => f.Value != null)
            .Select(f => $@"<tr>
                <td style='padding:8px 12px;color:{ColorTextLight};font-size:13px;white-space:nowrap;vertical-align:top;'>{f.Key}</td>
                <td style='padding:8px 12px;color:{ColorText};font-size:13px;font-weight:600;vertical-align:top;'>{f.Value}</td>
            </tr>"));

        return $@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background-color:{ColorInfoBg};border-radius:8px;margin:16px 0;border:1px solid {ColorBorder};'>
            {rows}
        </table>";
    }

    /// <summary>Builds a colored alert box (info, warning, success, danger).</summary>
    public static string BuildAlertBox(string message, string type = "info")
    {
        var (bgColor, borderColor, textColor) = type switch
        {
            "success" => ("#f0fdf4", "#bbf7d0", "#166534"),
            "warning" => ("#fffbeb", "#fde68a", "#92400e"),
            "danger" => ("#fef2f2", "#fecaca", "#991b1b"),
            _ => ("#eff6ff", "#bfdbfe", "#1e40af") // info
        };

        return $@"<div style='background-color:{bgColor};border:1px solid {borderColor};border-radius:8px;padding:12px 16px;margin:16px 0;'>
            <p style='margin:0;color:{textColor};font-size:13px;line-height:1.5;'>{message}</p>
        </div>";
    }

    /// <summary>Builds an orange accent horizontal divider.</summary>
    public static string BuildDivider()
    {
        return $@"<div style='border:none;border-top:2px solid {ColorAccent};margin:24px 0;opacity:0.3;'></div>";
    }

    /// <summary>Greeting line: "Hello/Bonjour {name},"</summary>
    public static string Greeting(string name, EmailLanguage language)
    {
        return $"<p style='margin:0 0 16px;font-size:15px;'>{L("Hello", "Bonjour", language)} {name},</p>";
    }

    /// <summary>Paragraph with standard margin.</summary>
    public static string P(string text)
    {
        return $"<p style='margin:0 0 12px;font-size:15px;line-height:1.6;'>{text}</p>";
    }
}
