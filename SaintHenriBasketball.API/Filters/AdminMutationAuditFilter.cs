using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.API.Filters;

/// Records every successful admin-only change (POST, PUT, PATCH, DELETE) that didn't already write its own,
/// more detailed audit entry, so jobs, promo codes, templates, seasons, waivers and flags are on the record too.
public class AdminMutationAuditFilter(ILogger<AdminMutationAuditFilter> logger) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        var requiresAdmin = AdminAuditPolicy.RequiresAdmin(
            context.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Select(a => a.Roles));
        var skipped = context.ActionDescriptor.EndpointMetadata.OfType<SkipAdminAuditAttribute>().Any();
        if (skipped || !requiresAdmin || !AdminAuditPolicy.IsMutating(method))
        {
            await next();
            return;
        }

        var startedAt = DateTime.UtcNow;
        var executed = await next();
        if (executed.Exception != null && !executed.ExceptionHandled) return;
        if (!AdminAuditPolicy.ShouldAudit(method, requiresAdmin, context.HttpContext.Response.StatusCode)) return;

        var path = context.HttpContext.Request.Path.Value ?? "";
        try
        {
            var services = context.HttpContext.RequestServices;
            var userId = context.HttpContext.User.AuditUserId();
            var userName = context.HttpContext.User.AuditUserName();

            // Endpoints that already audit themselves (refunds, cancellations, account changes...) keep their own entry.
            var db = services.GetRequiredService<ApplicationDbContext>();
            if (await db.AuditLogs.AnyAsync(a => a.CreatedAt >= startedAt && (a.UserId == userId || a.UserName == userName))) return;

            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var entry = AdminAuditPolicy.Describe(
                descriptor?.ControllerName ?? "Admin",
                descriptor?.ActionName ?? "Change",
                context.RouteData.Values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value?.ToString())),
                method,
                path);
            await services.GetRequiredService<IAuditLogService>()
                .LogAsync(entry.Action, entry.EntityType, entry.EntityId, entry.Details, userId, userName);
        }
        catch (Exception ex)
        {
            // The change itself succeeded; a missing audit row must not turn it into an error.
            logger.LogWarning(ex, "Could not write the automatic audit entry for {Method} {Path}", method, path);
        }
    }
}
