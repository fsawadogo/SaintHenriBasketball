using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SaintHenriBasketball.Infrastructure.Jobs;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Hangfire services
        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));

        // Add Hangfire server
        services.AddHangfireServer();

        return services;
    }

    public static IApplicationBuilder UseHangfireJobs(this IApplicationBuilder app)
    {
        // Configure recurring jobs
        HangfireJobsConfiguration.ConfigureRecurringJobs();
        app.UseHangfireDashboard("/hangfire");

        //if (env.IsDevelopment())
        //{
        //    // In development, allow all access
        //    app.UseHangfireDashboard("/hangfire");
        //}
        //else
        //{
        //    // In production, restrict access to admins
        //    app.UseHangfireDashboard("/hangfire", new DashboardOptions
        //    {
        //        Authorization = new[] { new HangfireAuthorizationFilter() }
        //    });
        //}

        return app;
    }
}
