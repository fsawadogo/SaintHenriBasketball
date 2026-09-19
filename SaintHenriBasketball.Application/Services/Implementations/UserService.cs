using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http;
using System.Text;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using Google.Apis.Auth;
using SaintHenriBasketball.Application.FeatureFlags;
using ValidationException = SaintHenriBasketball.Application.Exceptions.ValidationException;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class UserService : IUserService
{
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IReferralRepository _referralRepository;

    public const string InvalidReferralCodeMessage = "Referral code not found or no longer valid.";
    public const string DeactivatedAccountMessage = "This account is deactivated. Contact the club if you think this is a mistake.";

    public UserService(
        IConfiguration configuration,
        IMapper mapper,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<UserService> logger,
        IFeatureFlagService featureFlagService,
        IReferralRepository referralRepository,
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _mapper = mapper;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
        _featureFlagService = featureFlagService;
        _referralRepository = referralRepository;
    }

    /// Through a factory rather than `new HttpClient()`: it makes the Google checks testable, and
    /// avoids a fresh socket per sign-in.
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task<UserResponseDto> RegisterAsync(RegisterUserDto registerDto)
    {
        if (await _userRepository.EmailExistsAsync(registerDto.Email))
        {
            throw new ValidationException("Email is already registered");
        }

        if (await _userRepository.UsernameExistsAsync(registerDto.Username))
        {
            throw new ValidationException("Username is already taken");
        }

        // Signing up is the only chance to redeem: login needs a confirmed email first.
        ReferralCode? referralCode = null;
        if (!string.IsNullOrWhiteSpace(registerDto.ReferralCode)
            && await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Referrals))
        {
            referralCode = await _referralRepository.GetCodeByValueAsync(registerDto.ReferralCode.Trim().ToUpperInvariant());
            if (referralCode is null || !referralCode.IsActive || (referralCode.MaxUses is int maxUses && referralCode.TimesUsed >= maxUses))
                throw new ValidationException(InvalidReferralCodeMessage);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

        var user = new ApplicationUser(
            registerDto.Username,
            registerDto.Email,
            passwordHash,
            registerDto.FirstName,
            registerDto.LastName,
            registerDto.PaymentPlan
        );

        // Generate and set email confirmation token
        user.EmailConfirmationToken = Guid.NewGuid().ToString("N");

        // A new signup never starts on the season plan, whatever the request body said.
        // PaymentPlan.Season is 0, so an omitted field bound to it — and because a season-plan
        // profile counts as a pass spot, anonymous signups could quietly sell the season out.
        // Taking a pass goes through SeasonPlanService, which counts and claims under a lock.
        user.PaymentPlan = PaymentPlan.DropIn;

        if (referralCode is null)
        {
            await _userRepository.AddAsync(user);
        }
        else
        {
            // The account, the Pending redemption and the code's use count commit together or not at all.
            var outcome = await _referralRepository.TryRedeemAsync(
                new ReferralRedemption(referralCode.Id, referralCode.OwnerUserId, user.Id), user);
            if (outcome != ReferralRedeemOutcome.Redeemed)
                throw new ValidationException(InvalidReferralCodeMessage);
            _logger.LogInformation("New user {UserId} registered with referral code {Code}", user.Id, referralCode.Code);
        }

        try
        {
            // Send confirmation email
            var confirmationLink = $"{_configuration["AppUrl"]}/confirm-email?token={user.EmailConfirmationToken}&email={user.Email}";
            await _emailService.SendConfirmationEmailAsync(user.Email, confirmationLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", user.Email);
            // Continue with registration even if email fails
        }

        return new UserResponseDto
        {
            Token = GenerateJwtToken(user),
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin,
            StaffRole = user.StaffRole,
            PaymentPlan = user.PaymentPlan
        };
    }

    public async Task<UserResponseDto> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetByUsernameAsync(loginDto.UserName);

        if (user == null)
        {
            throw new ValidationException("Invalid credentials");
        }

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid credentials");
        }

        if (!user.EmailConfirmed)
        {
            throw new ValidationException("Please confirm your email before logging in");
        }

        if (user.IsDeactivated)
        {
            throw new ValidationException(DeactivatedAccountMessage);
        }

        var requires2Fa = await RequiresTwoFactorAsync(user);
        var requires2FaSetup = !requires2Fa && await RequiresTwoFactorSetupAsync(user);

        return new UserResponseDto
        {
            Token = GenerateJwtToken(user, twoFactorPending: requires2Fa, twoFactorEnrollment: requires2FaSetup),
            Requires2FaSetup = requires2FaSetup,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin,
            StaffRole = user.StaffRole,
            PaymentPlan = user.PaymentPlan,
            Requires2Fa = requires2Fa,
        };
    }

    public const string InvalidGoogleTokenMessage = "Invalid Google token";
    public const string GoogleNotConfiguredMessage = "Google sign-in is not configured.";

    public async Task<UserResponseDto> GoogleLoginAsync(string accessToken)
    {
        // A Google access token is a bearer credential that is NOT bound to whoever receives it: a
        // token minted for any other OAuth client, for the same user, is a perfectly valid token.
        // Asking userinfo "who is this?" therefore proves nothing about who is asking. Only the
        // audience does — it names the client the token was issued to, and it must be this app.
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            // Fail closed: without a client id there is nothing to check the token against, and
            // accepting it anyway is the whole vulnerability.
            _logger.LogError("Google sign-in attempted with no Google:ClientId configured");
            throw new ValidationException(GoogleNotConfiguredMessage);
        }

        string? email, givenName, familyName;
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var tokenInfo = await httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?access_token={Uri.EscapeDataString(accessToken)}");
            if (!tokenInfo.IsSuccessStatusCode) throw new ValidationException(InvalidGoogleTokenMessage);

            using var tokenDoc = System.Text.Json.JsonDocument.Parse(await tokenInfo.Content.ReadAsStringAsync());
            var tokenRoot = tokenDoc.RootElement;

            var audience = tokenRoot.TryGetProperty("aud", out var aud) ? aud.GetString() : null;
            if (!string.Equals(audience, clientId, StringComparison.Ordinal))
            {
                _logger.LogWarning("Google sign-in refused: token was issued to {Audience}, not this app", audience);
                throw new ValidationException(InvalidGoogleTokenMessage);
            }

            // Google will hand out a token for an address the account has not proven it owns.
            // Trusting one would let someone sign in as a member by claiming their address.
            var verified = tokenRoot.TryGetProperty("email_verified", out var ev)
                && (ev.ValueKind == System.Text.Json.JsonValueKind.True
                    || string.Equals(ev.GetString(), "true", StringComparison.OrdinalIgnoreCase));
            if (!verified)
            {
                _logger.LogWarning("Google sign-in refused: the address on the token is not verified");
                throw new ValidationException(InvalidGoogleTokenMessage);
            }

            email = tokenRoot.TryGetProperty("email", out var em) ? em.GetString() : null;

            // Names only; the identity above is already settled.
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
            response.EnsureSuccessStatusCode();
            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            givenName = root.TryGetProperty("given_name", out var gn) ? gn.GetString() : "";
            familyName = root.TryGetProperty("family_name", out var fn) ? fn.GetString() : "";
        }
        catch (HttpRequestException)
        {
            throw new ValidationException(InvalidGoogleTokenMessage);
        }

        if (string.IsNullOrEmpty(email))
            throw new ValidationException("Google account has no email");

        var user = await _userRepository.GetByEmailAsync(email);

        if (user == null)
        {
            var username = email.Split('@')[0];
            if (await _userRepository.UsernameExistsAsync(username))
            {
                username = $"{username}{Random.Shared.Next(1000, 9999)}";
            }

            user = new ApplicationUser(
                username: username,
                email: email,
                passwordHash: "",
                firstName: givenName ?? "",
                lastName: familyName ?? "",
                paymentPlan: PaymentPlan.DropIn
            );
            user.EmailConfirmed = true;
            user.EmailConfirmationToken = "";

            await _userRepository.AddAsync(user);
        }
        else if (user.IsDeactivated)
        {
            throw new ValidationException(DeactivatedAccountMessage);
        }

        // Google sign-in gets the same second step as a password sign-in.
        var requires2Fa = await RequiresTwoFactorAsync(user);
        var requires2FaSetup = !requires2Fa && await RequiresTwoFactorSetupAsync(user);

        return new UserResponseDto
        {
            Token = GenerateJwtToken(user, twoFactorPending: requires2Fa, twoFactorEnrollment: requires2FaSetup),
            Requires2FaSetup = requires2FaSetup,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin,
            StaffRole = user.StaffRole,
            PaymentPlan = user.PaymentPlan,
            Requires2Fa = requires2Fa,
        };
    }

    private async Task<bool> RequiresTwoFactorAsync(ApplicationUser user) =>
        user.IsAdmin && user.TwoFactorEnabled && await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Admin2fa);

    private async Task<bool> RequiresTwoFactorSetupAsync(ApplicationUser user) =>
        user.IsAdmin && !user.TwoFactorEnabled && await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Admin2fa);

    public async Task<UserDto> GetUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> GetUserByEmailAsync(string? email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        return _mapper.Map<UserDto>(user);
    }
    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto updateDto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found");
        }

        if (!string.IsNullOrEmpty(updateDto.Email) && updateDto.Email != user.Email)
        {
            if (await _userRepository.EmailExistsAsync(updateDto.Email))
            {
                throw new ValidationException("Email is already taken");
            }
            user.Email = updateDto.Email;
        }

        if (!string.IsNullOrEmpty(updateDto.Username) && updateDto.Username != user.Username)
        {
            if (await _userRepository.UsernameExistsAsync(updateDto.Username))
            {
                throw new ValidationException("Username is already taken");
            }
            user.Username = updateDto.Username;
        }

        user.FirstName = updateDto.FirstName ?? user.FirstName;
        user.LastName = updateDto.LastName ?? user.LastName;
        if (updateDto.PaymentPlan is { } paymentPlan)
            user.PaymentPlan = paymentPlan;

        await _userRepository.UpdateAsync(user);
        return _mapper.Map<UserDto>(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllUsersAsync();
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task ConfirmEmailAsync(string? email, string token)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            throw new ValidationException("Invalid email");
        }

        if (user.EmailConfirmationToken != token)
        {
            throw new ValidationException("Invalid confirmation token");
        }

        if (user.EmailConfirmed)
        {
            throw new ValidationException("Email already confirmed");
        }

        user.EmailConfirmed = true;
        user.EmailConfirmationToken = null!;
        
        await _userRepository.UpdateAsync(user);
    }

    public Task ForgotPasswordAsync(string? email) => SendPasswordLinkAsync(email, TimeSpan.FromHours(1));

    /// A longer-lived set-your-password link for accounts an admin created or imported.
    public Task SendSetPasswordInviteAsync(string email, TimeSpan validFor) => SendPasswordLinkAsync(email, validFor);

    private async Task SendPasswordLinkAsync(string? email, TimeSpan validFor)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        // Don't reveal whether the account exists, and never reopen a deactivated account by email.
        if (user == null || user.IsDeactivated)
        {
            return;
        }

        user.PasswordResetToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        user.PasswordResetTokenExpiry = DateTime.UtcNow.Add(validFor);

        await _userRepository.UpdateAsync(user);

        var resetLink = $"{_configuration["AppUrl"]}/reset-password?token={user.PasswordResetToken}&email={Uri.EscapeDataString(user.Email!)}";
        await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        var user = await _userRepository.GetByEmailAsync(resetPasswordDto.Email);
        if (user == null)
        {
            throw new ValidationException("Invalid email");
        }

        if (user.PasswordResetToken != resetPasswordDto.Token)
        {
            throw new ValidationException("Invalid reset token");
        }

        if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            throw new ValidationException("Reset token has expired");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
        user.PasswordResetToken = null!;
        user.PasswordResetTokenExpiry = null;

        await _userRepository.UpdateAsync(user);
    }

    public async Task UpdateUserPaymentPlanAsync(Guid userId, PaymentPlan paymentPlan)
    {
        try
        {
            _logger.LogInformation("Attempting to update payment plan for user {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User with ID {userId} not found");
            }

            // Check if the payment plan is actually changing
            if (user.PaymentPlan == paymentPlan)
            {
                _logger.LogInformation("Payment plan unchanged for user {UserId}", userId);
                return;
            }

            // Update the payment plan
            user.PaymentPlan = paymentPlan;
            await _userRepository.UpdateAsync(user);

            // Send email notification
            try
            {
                await _emailService.SendPaymentPlanUpdateEmailAsync( user.Id, paymentPlan);
            }
            catch (Exception ex)
            {
                // Log but don't throw - email notification is not critical
                _logger.LogWarning(ex, "Failed to send payment plan update email to user {UserId}", userId);
            }

            _logger.LogInformation("Successfully updated payment plan for user {UserId} to {PaymentPlan}",
                userId, paymentPlan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment plan for user {UserId}", userId);
            throw;
        }
    }

    public async Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string?> emails, EmailLanguage language, string? customMessage, string? customMessageFr = null)
    {
        try
        {
            _logger.LogInformation("Starting to send {EmailType} emails to {Count} recipients in {Language}",
                emailType, emails.Count, language);

            if (emails.Count == 0)
            {
                throw new ValidationException("No email addresses provided");
            }

            // Validate all email addresses
            var invalidEmails = emails.Where(e => !new EmailAddressAttribute().IsValid(e)).ToList();
            if (invalidEmails.Count != 0)
            {
                throw new ValidationException($"Invalid email addresses: {string.Join(", ", invalidEmails)}");
            }

            var result = new EmailSendResult();

            foreach (var email in emails)
            {
                try
                {
                    // Try to get the user to ensure they exist in our system
                    var user = await _userRepository.GetByEmailAsync(email);

                    // Send the targeted email
                    if (email != null)
                    {
                        await _emailService.SendTargetedEmailsAsync(
                            emailType,
                            [email],
                            language,
                            customMessage,
                            customMessageFr);

                        result.SuccessCount++;
                        _logger.LogInformation("Successfully sent {EmailType} email to {Email}", emailType, email);
                    }
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.FailedEmails?.Add(email);
                    _logger.LogError(ex, "Failed to send {EmailType} email to {Email}", emailType, email);
                }
            }

            // Log final results
            _logger.LogInformation(
                "Completed sending {EmailType} emails. Success: {SuccessCount}, Failed: {FailureCount}",
                emailType, result.SuccessCount, result.FailureCount);

            if (result.FailedEmails != null && result.FailedEmails.Count != 0)
            {
                _logger.LogWarning(
                    "Failed to send emails to: {FailedEmails}",
                    string.Join(", ", result.FailedEmails));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendTargetedEmailsAsync");
            throw;
        }
    }
    private string GenerateJwtToken(ApplicationUser user, bool twoFactorPending = false, bool twoFactorEnrollment = false)
    {
        if (user is not { Email: not null, Username: not null })
        {
            throw new ValidationException("User email and username are required");
        }

        var jwtKey = _configuration["JwtSettings:Key"]
                     ?? throw new InvalidOperationException("JWT key is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        // Volunteer role (see StaffAccess); checked against the account on every request, like the Admin role.
        if (Helpers.StaffAccess.ClaimValue(user.StaffRole) is { } staffRole)
            claims.Add(new Claim(Helpers.StaffAccess.ClaimType, staffRole));

        // Short-lived pending-2FA tokens carry this claim; middleware blocks all requests
        // except the 2FA verify/setup endpoints until the user exchanges it.
        if (twoFactorPending)
            claims.Add(new Claim("2fa_pending", "true"));
        // Admins who must set up 2FA first get a short session that only allows setup (see TwoFactorPendingMiddleware).
        if (twoFactorEnrollment)
            claims.Add(new Claim("2fa_enroll", "true"));

        var durationInDays = twoFactorPending || twoFactorEnrollment
            ? (1.0 / 96.0) // 15 minutes
            : Convert.ToDouble(_configuration["JwtSettings:DurationInDays"] ?? throw new InvalidOperationException("JWT duration is not configured"));

        if (durationInDays <= 0)
        {
            throw new InvalidOperationException("JWT duration must be positive");
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(durationInDays),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> IssueTokenAsync(Guid userId, bool twoFactorPending = false)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found");
        return GenerateJwtToken(user, twoFactorPending);
    }
}