using Hangfire.Dashboard;

namespace SaintHenriBasketball.Infrastructure.Jobs;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Only allow authenticated admin users to access the dashboard
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
        var isAdmin = httpContext.User.IsInRole("Admin");

        return isAuthenticated && isAdmin;
    }
}
