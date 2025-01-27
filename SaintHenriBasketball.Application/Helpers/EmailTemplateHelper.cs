namespace SaintHenriBasketball.Application.Helpers;

public static class EmailTemplateHelper
{
    public static class Styles
    {
        public const string Container = "font-family: Arial, sans-serif; margin: 0; padding: 20px;";
        public const string MainDiv = "max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);";
        public const string Title = "color: #333; text-align: center;";
        public const string Content = "color: #666; font-size: 16px;";
        public const string InfoBox = "background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;";
        public const string InfoText = "margin: 5px 0; color: #444;";
        public const string Button = "background-color: #4CAF50; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;";
        public const string Divider = "border: none; border-top: 1px solid #eee; margin: 20px 0;";
        public const string Footer = "color: #999; font-size: 12px; text-align: center;";
    }

    public static string BuildEmailLayout(string title, string content, bool includeFooter = true) =>
        $@"<html>
            <body style='{Styles.Container}'>
                <div style='{Styles.MainDiv}'>
                    <h1 style='{Styles.Title}'>{title}</h1>
                    {content}
                    {(includeFooter ? GetFooter() : string.Empty)}
                </div>
            </body>
        </html>";

    public static string BuildInfoBox(Dictionary<string, string?> fields) =>
        $@"<div style='{Styles.InfoBox}'>
            {string.Join("\n", fields.Select(f => 
                $"<p style='{Styles.InfoText}'><strong>{f.Key}:</strong> {f.Value}</p>"))}
        </div>";

    public static string BuildButton(string text, string url) =>
        $@"<div style='text-align: center; margin: 30px 0;'>
            <a href='{url}' style='{Styles.Button}'>{text}</a>
        </div>";

    public static string BuildBilingualContent(string titleEn, string titleFr, string contentEn, string contentFr) =>
        BuildEmailLayout(
            $"{titleEn} / {titleFr}",
            $@"<div style='{Styles.InfoBox}'>
                <h2 style='{Styles.Title}'>English</h2>
                {contentEn}
            </div>
            <div style='{Styles.InfoBox}'>
                <h2 style='{Styles.Title}'>Français</h2>
                {contentFr}
            </div>"
        );

    private static string GetFooter() =>
        $@"<hr style='{Styles.Divider}'>
            <div style='{Styles.Footer}'>
                <p>Saint Henri Basketball</p>
                <p>717 Saint-Ferdinand Street, Montreal, QC H4C 3L7</p>
                <p>(438) 935-8129 | info@sainthenribasketball.com</p>
            </div>";
}