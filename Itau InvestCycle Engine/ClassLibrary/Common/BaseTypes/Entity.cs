namespace Itau.InvestCycleEngine.Common.BaseTypes;

public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
        => obj is Entity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}
