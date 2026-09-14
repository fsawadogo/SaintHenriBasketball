using Quartz;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class WaitlistOfferJob(IWaitlistService waitlist) : IJob
{
    public Task Execute(IJobExecutionContext context) => waitlist.ProcessOffersAsync();
}
