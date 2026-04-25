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
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly ISessionAttendanceRepository _attendanceRepository;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<QrCheckInService> _logger;

    public QrCheckInService(
        IConfiguration configuration,
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository registrationRepository,
        ISessionAttendanceRepository attendanceRepository,
        IPaymentService paymentService,
        ILogger<QrCheckInService> logger)
    {
        _configuration = configuration;
        _sessionRepository = sessionRepository;
        _registrationRepository = registrationRepository;
        _attendanceRepository = attendanceRepository;
        _paymentService = paymentService;
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

        var isRegistered = await _registrationRepository.IsUserRegisteredAsync(userId, sessionId);
        if (!isRegistered)
            throw new ValidationException("You are not registered for this session");

        // Billing failure must never block attendance — the 11 AM cron retries idempotently.
        try
        {
            await _paymentService.EnsureDropInPaymentForSessionAsync(userId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-bill on QR check-in failed for user {UserId} session {SessionId}", userId, sessionId);
        }

        var now = DateTime.UtcNow;
        var existing = await _attendanceRepository.GetAttendanceAsync(sessionId, userId);
        if (existing is null)
        {
            await _attendanceRepository.AddAsync(new SessionAttendance
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                IsAttending = true,
                CheckInTime = now,
                CreatedOn = now,
                LastUpdated = now,
            });
        }
        else if (!existing.IsAttending || existing.CheckInTime is null)
        {
            existing.IsAttending = true;
            existing.CheckInTime = now;
            existing.LastUpdated = now;
            await _attendanceRepository.UpdateAsync(existing);
        }

        _logger.LogInformation("QR check-in: user {UserId} → session {SessionId}", userId, sessionId);
        return new QrCheckInResultDto { SessionId = sessionId, CheckedInAt = now };
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
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
