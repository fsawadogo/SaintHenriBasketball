using Microsoft.Extensions.Configuration;

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

    private readonly IParticipationRepository _participation;
    private readonly IWaiverService _waiverService;

    private readonly IConfiguration _configuration;

    private readonly INotificationService _notifications;

    private readonly IWaitlistRepository _waitlistRepository;

    private readonly ISessionRepository _sessionRepository;

    private readonly IUserRepository _userRepository;

    private readonly IEmailService _emailService;

    private readonly ILogger<WaitlistService> _logger;



    public WaitlistService(

        IParticipationRepository participation, IConfiguration configuration, INotificationService notifications, IWaiverService waiverService,

        IWaitlistRepository waitlistRepository,

        ISessionRepository sessionRepository,

        IUserRepository userRepository,

        IEmailService emailService,

        ILogger<WaitlistService> logger)

    {

        _participation = participation; _configuration = configuration; _notifications = notifications; _waiverService = waiverService;

        _waitlistRepository = waitlistRepository;

        _sessionRepository = sessionRepository;

        _userRepository = userRepository;

        _emailService = emailService;

        _logger = logger;

    }



    public async Task<WaitlistDto> JoinWaitlistAsync(Guid userId, JoinWaitlistDto request)

    {

        await _waiverService.EnsureAcceptedAsync(userId);
        var entry = await _participation.JoinWaitlistAsync(request.SessionId, userId, request.Notes);

        await PromoteNextAsync(request.SessionId);

        var user = await _userRepository.GetByIdAsync(userId);

        return MapToDto(entry, user);

    }



    public async Task LeaveWaitlistAsync(Guid userId, Guid sessionId)

    {

        await _participation.LeaveWaitlistAsync(sessionId, userId);
        await PromoteNextAsync(sessionId);



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

        var next = await _participation.OfferNextAsync(sessionId);

        if (next == null) return;
        await NotifyOfferAsync(sessionId, next);
    }

    public async Task<WaitlistOfferOutcome> OfferEntryAsync(Guid entryId)
    {
        var (entry, hadOpenSpot) = await _participation.OfferEntryAsync(entryId);
        await NotifyOfferAsync(entry.SessionId, entry);
        _logger.LogInformation("Admin offered waitlist entry {EntryId} for session {SessionId} (open spot: {HadOpenSpot})", entry.Id, entry.SessionId, hadOpenSpot);
        return new WaitlistOfferOutcome(entry.Id, entry.SessionId, hadOpenSpot, DateTime.SpecifyKind(entry.OfferExpiresAt!.Value, DateTimeKind.Utc));
    }

    private async Task NotifyOfferAsync(Guid sessionId, Waitlist next)
    {
        var url = $"{(_configuration["AppUrl"] ?? "https://sainthenribasketball.com").TrimEnd('/')}/sessions/{sessionId}/book";

        var deadline = Application.Helpers.SessionTimeHelper.ToLocal(next.OfferExpiresAt!.Value).ToString("yyyy-MM-dd HH:mm");

        await _notifications.CreateAsync(next.UserId, NotificationType.WaitlistOffer, "A spot is ready / Une place vous attend",
            $"Claim before {deadline} (Montreal) / Réservez avant {deadline} (Montréal).", $"/sessions/{sessionId}/book");

        // Notify the user that a spot is available

        try

        {

            var user = next.User ?? await _userRepository.GetByIdAsync(next.UserId);

            if (user?.Email != null && user.EmailNotificationsEnabled && user.WaitlistAlertsEnabled)

            {

                await _emailService.SendEmailAsync(

                    user.Email,

                    "Your waitlist place / Votre place sur la liste",

                    $"A place is reserved for you until {deadline} (Montreal). Claim it: {url} . Une place est réservée jusqu’au {deadline} (Montréal). Réservez : {url}");

            }

        }

        catch (Exception ex)

        {

            _logger.LogWarning(ex, "Failed to send waitlist promotion email for user {UserId}", next.UserId);

        }

    }



    public async Task ProcessOffersAsync()

    {

        foreach (var id in await _participation.GetWaitlistSessionIdsAsync())

            await PromoteNextAsync(id);

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

        OfferExpiresAt = entry.OfferExpiresAt,

    };

}
