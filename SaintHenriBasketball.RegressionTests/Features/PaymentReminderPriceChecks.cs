using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Resend;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

internal static class PaymentReminderPriceChecks
{
    public static async Task RunAsync(Func<ApplicationDbContext> db, Action<bool, string> assert)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var player = new ApplicationUser(
            $"reminder_{tag}",
            $"reminder-{tag}@example.test",
            "test-only",
            "Jessey",
            $"Reminder{tag}",
            PaymentPlan.Season)
        {
            EmailConfirmed = true,
            PreferredLanguage = EmailLanguage.French
        };

        var pending = new Payment(player.Id, 92.15m, PaymentPlan.Season)
        {
            OriginalAmount = 95m,
            DiscountAmount = 2.85m,
            Reference = $"SEASON-{tag}",
            CreatedAt = DateTime.UtcNow
        };
        var oldFailed = new Payment(player.Id, 95m, PaymentPlan.Season)
        {
            Status = PaymentStatus.Failed,
            Reference = $"FAILED-{tag}",
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        };

        await using var context = db();
        context.Users.Add(player);
        context.Payments.AddRange(pending, oldFailed);
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = "payment-reminder-regression-key",
                ["AppUrl"] = "https://sainthenribasketball.com",
                ["Resend:FromEmail"] = "test@sainthenribasketball.com",
                ["Resend:FromName"] = "Saint-Henri Basketball"
            })
            .Build();

        var resend = DispatchProxy.Create<IResend, RecordingResend>();
        var recorder = (RecordingResend)(object)resend;
        var service = new EmailService(
            new AttendanceLinks(configuration),
            configuration,
            NullLogger<EmailService>.Instance,
            new UserRepository(context, NullLogger<UserRepository>.Instance),
            new PaymentRepository(context),
            new TestWebHostEnvironment(),
            new SessionRepository(context),
            resend,
            new GenericRepository<EmailLog>(context),
            new UnsubscribeLinks(configuration));

        await service.SendPaymentReminderEmailAsync(player, PaymentPlan.Season);

        var html = recorder.Message?.HtmlBody ?? string.Empty;
        assert(html.Contains("$92.15", StringComparison.Ordinal),
            "payment reminder: the email shows the adjusted pending season balance");
        assert(!html.Contains("$95.00", StringComparison.Ordinal),
            "payment reminder: the fixed season list price does not replace an adjusted balance");
        assert(html.Contains($"SEASON-{tag}", StringComparison.Ordinal),
            "payment reminder: the amount and Interac reference come from the same pending payment");
        assert(!html.Contains($"FAILED-{tag}", StringComparison.Ordinal),
            "payment reminder: a newer failed payment is not treated as money still owed");
    }

    public class RecordingResend : DispatchProxy
    {
        public EmailMessage? Message { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IResend.EmailSendAsync))
                Message = args?.OfType<EmailMessage>().Single();

            if (targetMethod?.ReturnType.IsGenericType == true
                && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = targetMethod.ReturnType.GetGenericArguments()[0];
                var value = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, new[] { value });
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SaintHenriBasketball.RegressionTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}