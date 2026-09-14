using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SaintHenriBasketball.Application.DTOs.QrCheckIn;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class QrCheckInService : IQrCheckInService
{
    private const string Audience = "shb-qr-checkin";
    private const string SessionClaim = "session_id";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private readonly IConfiguration _configuration;
    private readonly IParticipationRepository _participation;
    private readonly ICacheService _cache;
    private readonly ISessionRepository _sessionRepository;
    private readonly IPaymentService _paymentService;
    private readonly IWaiverService _waiverService;
    private readonly ILogger<QrCheckInService> _logger;

    public QrCheckInService(
        IConfiguration configuration,
        IParticipationRepository participation,
        ICacheService cache,
        ISessionRepository sessionRepository,
        IPaymentService paymentService,
        IWaiverService waiverService,
        ILogger<QrCheckInService> logger)
    {
        _configuration = configuration;
        _participation = participation;
        _cache = cache;
        _sessionRepository = sessionRepository;
        _paymentService = paymentService;
        _waiverService = waiverService;
        _logger = logger;
    }

    public async Task<SessionQrTokenDto> GenerateTokenAsync(Guid sessionId, string checkInBaseUrl)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new NotFoundException($"Session {sessionId} not found");

        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);
        var token = WriteToken(sessionId, expiresAt);

        var url = $"{checkInBaseUrl.TrimEnd('/')}/check-in?token={Uri.EscapeDataString(token)}";
        return new SessionQrTokenDto
        {
            SessionId = session.Id,
            Token = token,
            CheckInUrl = url,
            ExpiresAt = expiresAt,
        };
    }

    public async Task<QrCheckInResultDto> CheckInAsync(Guid userId, string token)
    {
        var sessionId = ReadSessionId(token)
            ?? throw new ValidationException("Invalid or expired check-in token");
        await _waiverService.EnsureAcceptedAsync(userId);

        var attendance = await _participation.CheckInAsync(sessionId, userId);
        foreach (var key in new[] { $"Attendance:User:{userId}", $"Attendance:Session:{sessionId}", $"Attendance:Session:{sessionId}:Summary", $"Attendance:Session:{sessionId}:Attendees", $"Session_{sessionId}", "AvailableSessions", "UpcomingSessions" })
            await _cache.RemoveAsync(key);

        // Billing failure must never block attendance — the 11 AM cron retries idempotently.
        try
        {
            await _paymentService.EnsureDropInPaymentForSessionAsync(userId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-bill on QR check-in failed for user {UserId} session {SessionId}", userId, sessionId);
        }

        _logger.LogInformation("QR check-in: user {UserId} → session {SessionId}", userId, sessionId);
        // Stored as UTC; a repeat scan reads it back without a Kind, so mark it explicitly.
        return new QrCheckInResultDto { SessionId = sessionId, CheckedInAt = DateTime.SpecifyKind(attendance.CheckInTime!.Value, DateTimeKind.Utc) };
    }

    private string WriteToken(Guid sessionId, DateTime expiresAt)
    {
        var jwtKey = _configuration["JwtSettings:Key"]
                     ?? throw new InvalidOperationException("JWT key is not configured");
        var issuer = _configuration["JwtSettings:Issuer"];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: Audience,
            claims: new[] { new Claim(SessionClaim, sessionId.ToString()) },
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private Guid? ReadSessionId(string token)
    {
        var jwtKey = _configuration["JwtSettings:Key"];
        if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrWhiteSpace(token)) return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, parameters, out _);
            var raw = principal.FindFirst(SessionClaim)?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
        // Malformed input (e.g. not three JWT segments) throws SecurityTokenMalformedException,
        // an ArgumentException rather than a SecurityTokenException.
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}
