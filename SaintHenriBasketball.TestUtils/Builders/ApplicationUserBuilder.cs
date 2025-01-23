using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.TestUtils.Builders;

public class ApplicationUserBuilder
{
    private readonly ApplicationUser _user;

    public ApplicationUserBuilder()
    {
        _user = new ApplicationUser(
            username: "testuser",
            email: "test@example.com",
            passwordHash: "AQAAAAIAAYagAAAAELbKo2uE+7OG4LvXVoX/0V3jaSxTZGhLLgnS9vVZvbINSvjlOoJ7KbHfxp7mwEX2ww==",  // "Password123!"
            firstName: "Test",
            lastName: "User",
            paymentPlan: PaymentPlan.DropIn
        );
    }

    public ApplicationUserBuilder WithUsername(string username)
    {
        _user.Username = username;
        return this;
    }

    public ApplicationUserBuilder WithEmail(string email)
    {
        _user.Email = email;
        return this;
    }

    public ApplicationUserBuilder WithName(string firstName, string lastName)
    {
        _user.FirstName = firstName;
        _user.LastName = lastName;
        return this;
    }

    public ApplicationUserBuilder WithPaymentPlan(PaymentPlan plan)
    {
        _user.PaymentPlan = plan;
        return this;
    }

    public ApplicationUserBuilder AsAdmin(bool isAdmin = true)
    {
        _user.IsAdmin = isAdmin;
        return this;
    }

    public ApplicationUserBuilder WithEmailConfirmed(bool confirmed = true)
    {
        _user.EmailConfirmed = confirmed;
        return this;
    }

    public ApplicationUserBuilder WithEmailConfirmationToken(string token)
    {
        _user.EmailConfirmationToken = token;
        return this;
    }

    public ApplicationUserBuilder WithAdminRole()
    {
        _user.IsAdmin = true;
        return this;
    }


    public ApplicationUserBuilder WithPasswordResetToken(string token, DateTime expiry)
    {
        _user.PasswordResetToken = token;
        _user.PasswordResetTokenExpiry = expiry;
        return this;
    }

    public ApplicationUser Build() => _user;
}