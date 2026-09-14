using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// Delivers queued broadcasts one at a time, each in its own DI scope.
public class BroadcastWorker(BroadcastQueue queue, IServiceScopeFactory scopeFactory, ILogger<BroadcastWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var broadcast in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IBroadcastService>().DeliverAsync(broadcast);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Broadcast delivery failed for audience {Audience}", broadcast.Request.Audience);
            }
        }
    }
}
