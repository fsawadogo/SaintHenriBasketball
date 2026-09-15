namespace SaintHenriBasketball.API.Filters;

/// <summary>
/// Limits sessions that aren't fully signed in yet:
/// <list type="bullet">
/// <item>`2fa_pending` (password accepted, code not entered): only [SkipTwoFactorPendingCheck] endpoints; 401 otherwise.</item>
/// <item>`2fa_enroll` (an admin must set up 2FA first): only [AllowTwoFactorEnrollment] endpoints; 403 otherwise,
/// so the app doesn't treat it as an expired session and sign the admin out.</item>
/// </list>
/// ASP.NET rolls class- and action-level attributes into endpoint metadata.
/// </summary>
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
        if (user?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var metadata = context.GetEndpoint()?.Metadata;

        if (user.FindFirst("2fa_pending") is not null && metadata?.GetMetadata<SkipTwoFactorPendingCheckAttribute>() is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Two-factor verification required\",\"requires2Fa\":true}");
            return;
        }

        if (user.FindFirst("2fa_enroll") is not null && metadata?.GetMetadata<AllowTwoFactorEnrollmentAttribute>() is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Set up two-factor authentication to continue\",\"requires2FaSetup\":true}");
            return;
        }

        await _next(context);
    }
}
