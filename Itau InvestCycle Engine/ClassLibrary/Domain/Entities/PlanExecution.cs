using Itau.InvestCycleEngine.Common.Auditing;
using Itau.InvestCycleEngine.Domain.Enums;

namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class PlanExecution : AuditableEntity<Guid>
{
    public Guid PlanId { get; private set; }
    public DateTime RunAtUtc { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    private PlanExecution() { }

    public PlanExecution(Guid id, Guid planId, DateTime runAtUtc, ExecutionStatus status, string? errorMessage = null)
    {
        Id = id;
        PlanId = planId;
        RunAtUtc = runAtUtc;
        Status = status;
        ErrorMessage = errorMessage;
    }
}
