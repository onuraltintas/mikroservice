namespace EduPlatform.Shared.Kernel.Primitives;

/// <summary>
/// Base class for aggregate roots - the main entry point for a domain aggregate
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public int Version { get; protected set; }

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// AggregateRoot with Guid as Id
/// </summary>
public abstract class AggregateRoot : AggregateRoot<Guid>
{
    protected AggregateRoot() : base(Guid.NewGuid()) { }
    protected AggregateRoot(Guid id) : base(id) { }
}
