namespace SaintHenriBasketball.API.Filters;

/// Rejects any authenticated request whose JWT carries `2fa_pending: true`,
/// except endpoints decorated with [SkipTwoFactorPendingCheck] at the class or action level
/// (ASP.NET rolls both up into endpoint metadata).
public class TwoFactorPendingMiddleware
{
    private readonly RequestDelegate _next;

    public TwoFactorPendingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true || user.FindFirst("2fa_pending") is null)
        {
            await _next(context);
            return;
        }

        if (context.GetEndpoint()?.Metadata.GetMetadata<SkipTwoFactorPendingCheckAttribute>() is not null)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"message\":\"Two-factor verification required\",\"requires2Fa\":true}");
    }
}
