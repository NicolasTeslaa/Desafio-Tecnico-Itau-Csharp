using Itau.InvestCycleEngine.Common.Auditing;
using Itau.InvestCycleEngine.Domain.Enums;
using Itau.InvestCycleEngine.Domain.ValueObjects;

namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class ProgrammedPurchasePlan : AuditableEntity<Guid>
{
    public Guid AccountId { get; private set; }
    public Guid AssetId { get; private set; }
    public Money AmountPerRun { get; private set; }
    public PlanSchedule Schedule { get; private set; } = default!;
    public PlanStatus Status { get; private set; } = PlanStatus.Active;

    public DateTime NextRunAtUtc { get; private set; }

    private ProgrammedPurchasePlan() { }

    public ProgrammedPurchasePlan(
        Guid id,
        Guid accountId,
        Guid assetId,
        Money amountPerRun,
        PlanSchedule schedule,
        DateTime nextRunAtUtc)
    {
        Id = id;
        AccountId = accountId;
        AssetId = assetId;
        AmountPerRun = amountPerRun;
        Schedule = schedule;
        NextRunAtUtc = nextRunAtUtc;
    }

    public void Pause()
    {
        Status = PlanStatus.Paused;
        Touch();
    }

    public void Resume(DateTime nextRunAtUtc)
    {
        Status = PlanStatus.Active;
        NextRunAtUtc = nextRunAtUtc;
        Touch();
    }

    public void Cancel()
    {
        Status = PlanStatus.Cancelled;
        Touch();
    }

    public void SetNextRun(DateTime nextRunAtUtc)
    {
        NextRunAtUtc = nextRunAtUtc;
        Touch();
    }
}
