using System.Reflection;
using SaintHenriBasketball.Application.Services.Interfaces;

/// Captures the confirmation links AccountLifecycleService sends, so a resend can be checked
/// without a mail provider. Everything else on IEmailService is unexpected here.
public class LifecycleEmailRecorder : DispatchProxy
{
    public static readonly List<(string To, string Link)> Sent = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IEmailService.SendConfirmationEmailAsync))
        {
            Sent.Add(((string)args![0]!, (string)args[1]!));
            return Task.CompletedTask;
        }
        throw new NotSupportedException($"Unexpected email call: {targetMethod?.Name}");
    }
}
