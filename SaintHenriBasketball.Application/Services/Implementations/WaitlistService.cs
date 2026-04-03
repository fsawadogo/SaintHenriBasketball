using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Waitlist;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class WaitlistService : IWaitlistService
{
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<WaitlistService> _logger;

    public WaitlistService(
        IWaitlistRepository waitlistRepository,
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<WaitlistService> logger)
    {
        _waitlistRepository = waitlistRepository;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<WaitlistDto> JoinWaitlistAsync(Guid userId, JoinWaitlistDto request)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId);
        if (session == null)
            throw new NotFoundException($"Session {request.SessionId} not found");

        var existing = await _waitlistRepository.GetByUserAndSessionAsync(userId, request.SessionId);
        if (existing != null)
            throw new ValidationException("You are already on the waitlist for this session");

        var count = await _waitlistRepository.GetWaitlistCountAsync(request.SessionId);
        var entry = new Waitlist(userId, request.SessionId, count + 1);
        entry.Notes = request.Notes;

        await _waitlistRepository.AddAsync(entry);

        _logger.LogInformation("User {UserId} joined waitlist for session {SessionId} at position {Position}",
            userId, request.SessionId, entry.Position);

        var user = await _userRepository.GetByIdAsync(userId);
        return MapToDto(entry, user);
    }

    public async Task LeaveWaitlistAsync(Guid userId, Guid sessionId)
    {
        var entry = await _waitlistRepository.GetByUserAndSessionAsync(userId, sessionId);
        if (entry == null)
            throw new NotFoundException("Waitlist entry not found");

        entry.Status = WaitlistStatus.Cancelled;
        await _waitlistRepository.UpdateAsync(entry);

        _logger.LogInformation("User {UserId} left waitlist for session {SessionId}", userId, sessionId);
    }

    public async Task<IReadOnlyList<WaitlistDto>> GetSessionWaitlistAsync(Guid sessionId)
    {
        var entries = await _waitlistRepository.GetBySessionAsync(sessionId);
        return entries.Select(e => MapToDto(e, e.User)).ToList();
    }

    public async Task<WaitlistDto?> GetUserWaitlistEntryAsync(Guid userId, Guid sessionId)
    {
        var entry = await _waitlistRepository.GetByUserAndSessionAsync(userId, sessionId);
        if (entry == null) return null;

        var user = await _userRepository.GetByIdAsync(userId);
        return MapToDto(entry, user);
    }

    public async Task PromoteNextAsync(Guid sessionId)
    {
        var next = await _waitlistRepository.GetNextInLineAsync(sessionId);
        if (next == null) return;

        next.Status = WaitlistStatus.Offered;
        await _waitlistRepository.UpdateAsync(next);

        _logger.LogInformation("Promoted user {UserId} from waitlist for session {SessionId} (position {Position})",
            next.UserId, sessionId, next.Position);

        // Notify the user that a spot is available
        try
        {
            var user = next.User ?? await _userRepository.GetByIdAsync(next.UserId);
            if (user?.Email != null)
            {
                await _emailService.SendGeneralAnnouncementEmailAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}",
                    $"A spot has opened up for the basketball session! Log in to confirm your registration at https://sainthenribasketball.com/schedule");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send waitlist promotion email for user {UserId}", next.UserId);
        }
    }

    private static WaitlistDto MapToDto(Waitlist entry, ApplicationUser? user) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        SessionId = entry.SessionId,
        UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
        UserEmail = user?.Email,
        Position = entry.Position,
        Status = entry.Status,
        RegistrationDate = entry.RegistrationDate,
        Notes = entry.Notes,
    };
}
