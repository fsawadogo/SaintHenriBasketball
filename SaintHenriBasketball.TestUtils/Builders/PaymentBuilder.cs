using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.TestUtils.Builders;

public class PaymentBuilder
{
    private readonly Payment _payment;

    public PaymentBuilder()
    {
        _payment = new Payment(
            userId: Guid.NewGuid(),
            amount: 15.00m,
            plan: PaymentPlan.DropIn
        );
    }

    public PaymentBuilder ForUser(Guid userId)
    {
        _payment.UserId = userId;
        return this;
    }

    public PaymentBuilder WithAmount(decimal amount)
    {
        _payment.Amount = amount;
        return this;
    }

    public PaymentBuilder WithPlan(PaymentPlan plan)
    {
        _payment.Plan = plan;
        return this;
    }

    public PaymentBuilder WithStatus(PaymentStatus status)
    {
        _payment.Status = status;
        return this;
    }

    public PaymentBuilder WithPaymentDate(DateTime paymentDate)
    {
        _payment.PaymentDate = paymentDate;
        return this;
    }

    public PaymentBuilder WithUser(ApplicationUser user)
    {
        _payment.User = user;
        _payment.UserId = user.Id;
        return this;
    }

    public Payment Build() => _payment;
}
