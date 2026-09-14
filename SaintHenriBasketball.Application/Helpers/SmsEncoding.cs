namespace SaintHenriBasketball.Application.Helpers;

/// GSM 03.38 character set checks. Text outside it must be sent as Unicode, which
/// shortens each SMS segment from 160 to 70 characters.
public static class SmsEncoding
{
    private const string GsmBasic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?" +
        "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    private const string GsmExtension = "^{}\\[~]|€\f";

    /// True when the message contains a character GSM-7 cannot encode (e.g. French ô, ê, ç).
    public static bool RequiresUnicode(string message) =>
        message.Any(c => !GsmBasic.Contains(c) && !GsmExtension.Contains(c));
}
