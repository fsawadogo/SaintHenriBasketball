using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Filters;

/// Gates a controller or action behind a feature flag. Returns 404 (not 403) when the
/// flag is off so unreleased endpoints are indistinguishable from non-existent ones.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequireFeatureAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _flagKey;

    public RequireFeatureAttribute(string flagKey)
    {
        _flagKey = flagKey ?? throw new ArgumentNullException(nameof(flagKey));
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var service = context.HttpContext.RequestServices.GetService(typeof(IFeatureFlagService)) as IFeatureFlagService;
        if (service is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            return;
        }

        if (!await service.IsEnabledAsync(_flagKey))
        {
            context.Result = new NotFoundResult();
        }
    }
}
