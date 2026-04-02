using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class JobsController : ControllerBase
{
    private readonly ISchedulerFactory _schedulerFactory;

    public JobsController(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobs()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

        var jobs = new List<object>();
        foreach (var key in jobKeys)
        {
            var detail = await scheduler.GetJobDetail(key);
            var triggers = await scheduler.GetTriggersOfJob(key);
            var triggerInfos = new List<object>();

            foreach (var trigger in triggers)
            {
                var state = await scheduler.GetTriggerState(trigger.Key);
                triggerInfos.Add(new
                {
                    name = trigger.Key.Name,
                    cronExpression = (trigger as ICronTrigger)?.CronExpressionString,
                    description = trigger.Description,
                    nextFireTime = trigger.GetNextFireTimeUtc()?.UtcDateTime,
                    previousFireTime = trigger.GetPreviousFireTimeUtc()?.UtcDateTime,
                    status = state.ToString()
                });
            }

            jobs.Add(new
            {
                name = key.Name,
                group = key.Group,
                description = detail?.Description,
                jobType = detail?.JobType.Name,
                isDurable = detail?.Durable,
                triggers = triggerInfos
            });
        }

        return Ok(jobs);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetJob(string name)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name);
        var detail = await scheduler.GetJobDetail(jobKey);
        if (detail == null) return NotFound();

        var triggers = await scheduler.GetTriggersOfJob(jobKey);
        var triggerInfos = new List<object>();
        foreach (var trigger in triggers)
        {
            var state = await scheduler.GetTriggerState(trigger.Key);
            triggerInfos.Add(new
            {
                name = trigger.Key.Name,
                cronExpression = (trigger as ICronTrigger)?.CronExpressionString,
                description = trigger.Description,
                nextFireTime = trigger.GetNextFireTimeUtc()?.UtcDateTime,
                previousFireTime = trigger.GetPreviousFireTimeUtc()?.UtcDateTime,
                status = state.ToString()
            });
        }

        return Ok(new
        {
            name = jobKey.Name,
            group = jobKey.Group,
            description = detail.Description,
            jobType = detail.JobType.Name,
            triggers = triggerInfos
        });
    }

    [HttpPost("{name}/trigger")]
    public async Task<IActionResult> TriggerJob(string name)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name);
        if (!await scheduler.CheckExists(jobKey)) return NotFound();

        await scheduler.TriggerJob(jobKey);
        return Ok(new { message = $"Job '{name}' triggered" });
    }

    [HttpPost("{name}/pause")]
    public async Task<IActionResult> PauseJob(string name)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name);
        if (!await scheduler.CheckExists(jobKey)) return NotFound();

        await scheduler.PauseJob(jobKey);
        return Ok(new { message = $"Job '{name}' paused" });
    }

    [HttpPost("{name}/resume")]
    public async Task<IActionResult> ResumeJob(string name)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name);
        if (!await scheduler.CheckExists(jobKey)) return NotFound();

        await scheduler.ResumeJob(jobKey);
        return Ok(new { message = $"Job '{name}' resumed" });
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> UpdateJobSchedule(string name, [FromBody] UpdateJobDto dto)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name);
        if (!await scheduler.CheckExists(jobKey)) return NotFound();

        var triggers = await scheduler.GetTriggersOfJob(jobKey);
        foreach (var oldTrigger in triggers)
        {
            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(oldTrigger.Key)
                .ForJob(jobKey)
                .WithDescription(dto.Description ?? oldTrigger.Description)
                .WithCronSchedule(dto.CronExpression)
                .Build();

            await scheduler.RescheduleJob(oldTrigger.Key, newTrigger);
        }

        return Ok(new { message = $"Job '{name}' schedule updated to '{dto.CronExpression}'" });
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteJob(string name)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name);
        if (!await scheduler.CheckExists(jobKey)) return NotFound();

        await scheduler.DeleteJob(jobKey);
        return Ok(new { message = $"Job '{name}' deleted" });
    }
}

public class UpdateJobDto
{
    public required string CronExpression { get; set; }
    public string? Description { get; set; }
}
