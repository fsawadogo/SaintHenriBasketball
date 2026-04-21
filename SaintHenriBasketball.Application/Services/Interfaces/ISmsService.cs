namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISmsService
{
    /// Returns true on successful send, false otherwise. Implementations must not throw.
    Task<bool> SendAsync(string phoneNumber, string message);
    bool IsConfigured { get; }
}
