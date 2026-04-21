namespace SaintHenriBasketball.Application.DTOs.TwoFactor;

public class TwoFactorSetupDto
{
    public string Secret { get; set; } = string.Empty;
    public string OtpAuthUri { get; set; } = string.Empty;
}

public class TwoFactorCodeDto
{
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorVerifyResultDto
{
    public string Token { get; set; } = string.Empty;
}
