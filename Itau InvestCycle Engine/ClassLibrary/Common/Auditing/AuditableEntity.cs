using Itau.InvestCycleEngine.Common.BaseTypes;

namespace Itau.InvestCycleEngine.Common.Auditing;

public abstract class AuditableEntity<TId> : Entity<TId>
{
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; protected set; } = DateTime.UtcNow;

    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
