namespace SaintHenriBasketball.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedOn { get; protected set; }
    public DateTime? ModifiedOn { get; protected set; }
}
